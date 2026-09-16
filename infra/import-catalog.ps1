<#
.SYNOPSIS
    Importa un paquete de `export-catalog.ps1` en una base de datos.

.DESCRIPTION
    Hace las tres cosas de un traslado y comprueba la tercera:

      1. Descomprime `content.zip` en la raíz de contenido, que es de donde la API sirve las
         lecciones. Sin este paso el temario aparece entero y cada lección devuelve 404.
      2. Aplica `catalog.sql` en una transacción. Si algo falla, no queda nada a medias.
      3. Cuenta las filas del destino y las compara con `manifest.json`, tabla a tabla. Sin
         esta comparación «ha ido bien» es una impresión, no una comprobación.

    Es idempotente: el SQL del paquete usa ON CONFLICT DO NOTHING, así que importar dos veces
    no duplica. Tampoco actualiza lo que ya esté; para eso están los manifests de curso.

    No trae usuarios porque el paquete no los lleva. El destino arranca con el catálogo
    completo y sin un solo alumno.

.PARAMETER Package
    Carpeta del paquete: la que tiene catalog.sql, content.zip y manifest.json.

.PARAMETER Container
    Contenedor de Postgres de DESTINO. Por defecto se descubre con docker compose. Alternativa
    para una base que no está en Docker: -PgHost, -PgPort, -PgUser (y PGPASSWORD en el entorno).

.PARAMETER ContentRoot
    Dónde descomprimir el contenido. Por defecto `content/dist` del repositorio, que es lo que
    docker-compose monta en `/srv/content`.

.PARAMETER DryRun
    Dice qué haría y no toca nada.

.PARAMETER NoContent
    No descomprime el contenido. Solo el SQL.

.PARAMETER Yes
    No pregunta antes de escribir.

.EXAMPLE
    .\infra\import-catalog.ps1 -Package .\export\catalog-20260915-2330 -DryRun

.EXAMPLE
    .\infra\import-catalog.ps1 -Package .\export\catalog-20260915-2330

.EXAMPLE
    $env:PGPASSWORD = '...'
    .\infra\import-catalog.ps1 -Package .\paquete -PgHost db.interno -PgUser academy
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Package,

    [string]$Container,
    [string]$PgHost,
    [int]$PgPort = 5432,
    [string]$Database = 'academy',
    [string]$User = 'academy',

    [string]$ContentRoot,
    [switch]$DryRun,
    [switch]$NoContent,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Root = Split-Path $PSScriptRoot -Parent
$Compose = Join-Path $Root 'infra/docker-compose.yml'

function Write-Step($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }
function Write-Ok($m)   { Write-Host "  OK  $m" -ForegroundColor Green }
function Write-Warn($m) { Write-Host "  !   $m" -ForegroundColor Yellow }
function Write-Bad($m)  { Write-Host "  X   $m" -ForegroundColor Red }
function Write-Info($m) { Write-Host "      $m" -ForegroundColor DarkGray }

$utf8 = [System.Text.UTF8Encoding]::new($false)

# ── el paquete ───────────────────────────────────────────────────────────────────────────

if (-not (Test-Path $Package)) {
    throw "No existe el paquete: $Package"
}

$Package = (Resolve-Path $Package).Path
$sqlPath = Join-Path $Package 'catalog.sql'
$zipPath = Join-Path $Package 'content.zip'
$manifestPath = Join-Path $Package 'manifest.json'

if (-not (Test-Path $sqlPath)) {
    throw "El paquete no tiene catalog.sql: $Package"
}

# El manifest no es decorativo: sin él no hay contra qué comparar y la importación sería un
# «parece que sí». Se exige.
if (-not (Test-Path $manifestPath)) {
    throw @"
El paquete no tiene manifest.json, así que no hay forma de comprobar que llegó todo.

Si viene de una exportación antigua, vuelve a generarlo con infra/export-catalog.ps1. Si aun
así quieres aplicarlo, hazlo a mano y asume que nadie verificará el resultado:
  psql -U $User -d $Database -v ON_ERROR_STOP=1 -f "$sqlPath"
"@
}

$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$expected = [ordered]@{}
foreach ($property in $manifest.tables.PSObject.Properties) {
    $expected[$property.Name] = [int]$property.Value
}

Write-Step 'Paquete'
Write-Info "Origen     $($manifest.sourceDatabase), $($manifest.exportedAt)"
Write-Info ("Tablas     {0}, {1} filas" -f $expected.Count, (($expected.Values | Measure-Object -Sum).Sum))
Write-Info "Contenido  $($manifest.contentFiles) ficheros"

if ($manifest.includesSecrets) {
    Write-Warn 'Este paquete lleva la contraseña SMTP en claro. Bórralo del disco al terminar.'
}

# ── el destino ───────────────────────────────────────────────────────────────────────────
#
# Dos caminos: un contenedor (desarrollo, y el VPS con compose) o un psql contra un host. Se
# resuelven aquí, en un único sitio, y el resto del guion llama a Invoke-Psql sin saber cuál
# de los dos está usando.

$useDocker = $false

if ($PgHost) {
    if (-not (Get-Command psql -ErrorAction SilentlyContinue)) {
        throw 'Con -PgHost hace falta psql en el PATH.'
    }
} else {
    if (-not $Container) {
        if (Test-Path $Compose) {
            $Container = docker compose -f $Compose ps -q postgres 2>$null | Select-Object -First 1
        }
        if (-not $Container) {
            $Container = docker ps --filter 'ancestor=postgres:17-alpine' --format '{{.Names}}' |
                Select-Object -First 1
        }
    }

    if (-not $Container) {
        throw 'No hay destino: ni contenedor de Postgres ni -PgHost. Arranca el entorno o pasa uno de los dos.'
    }

    $useDocker = $true
}

function Invoke-Psql {
    param([string[]]$PsqlArgs)

    if ($useDocker) {
        docker exec $Container psql -U $User -d $Database @PsqlArgs
    } else {
        psql -h $PgHost -p $PgPort -U $User -d $Database @PsqlArgs
    }
}

$targetLabel = if ($useDocker) { "contenedor $Container · base '$Database'" } else { "$PgHost`:$PgPort · base '$Database'" }
Write-Step "Destino: $targetLabel"

# ── ¿está el esquema? ────────────────────────────────────────────────────────────────────
#
# Se comprueba antes de tocar nada. Un `catalog.sql` contra una base sin migrar falla en el
# primer INSERT, y el mensaje de psql («relation "product" does not exist») no dice lo que
# hay que hacer. Este sí.

$tableList = ($expected.Keys | ForEach-Object { "'$_'" }) -join ','
$presentRaw = Invoke-Psql @('-tA', '-c', "SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'public' AND c.relkind = 'r' AND c.relname IN ($tableList)")

if ($LASTEXITCODE -ne 0) {
    throw "No se puede consultar la base de destino. ¿Está levantada y es '$Database'?"
}

$present = @($presentRaw | Where-Object { $_ -and $_.Trim() } | ForEach-Object { $_.Trim() })
$absent = @($expected.Keys | Where-Object { $present -notcontains $_ })

if ($absent.Count -gt 0) {
    throw @"
La base de destino no tiene estas tablas: $($absent -join ', ').

Aplica primero las migraciones y vuelve a lanzar la importación:
  dotnet run --project api/src/Inkoova.Academy.Migrator

Este paquete lleva datos, no estructura.
"@
}

Write-Ok "Esquema al día: las $($expected.Count) tablas del paquete existen."

# ── estado previo ────────────────────────────────────────────────────────────────────────

function Get-Counts {
    $query = ($expected.Keys | ForEach-Object { "SELECT '$_' AS t, count(*) AS n FROM $_" }) -join ' UNION ALL '
    $raw = Invoke-Psql @('-tA', '-F|', '-c', $query)

    if ($LASTEXITCODE -ne 0) {
        throw 'No se pueden contar las filas del destino.'
    }

    $result = [ordered]@{}
    foreach ($line in $raw) {
        if (-not $line -or -not $line.Trim()) { continue }
        $parts = $line.Trim() -split '\|'
        $result[$parts[0]] = [int]$parts[1]
    }
    $result
}

$before = Get-Counts
$occupied = @($before.GetEnumerator() | Where-Object { $_.Value -gt 0 })

Write-Step 'Qué va a pasar'

foreach ($entry in $expected.GetEnumerator()) {
    $now = $before[$entry.Key]
    $note = if ($now -eq 0) { '' } elseif ($now -ge $entry.Value) { '  (ya tiene filas; ON CONFLICT no las tocará)' } else { '  (ya tiene filas)' }
    Write-Info ("{0,-20} destino {1,6}  +  paquete {2,6}{3}" -f $entry.Key, $now, $entry.Value, $note)
}

if ($occupied.Count -gt 0) {
    Write-Host ''
    Write-Warn "El destino no está vacío: $($occupied.Count) tablas con filas."
    Write-Info 'La importación AÑADE lo que falte y no toca lo que ya existe (ON CONFLICT DO NOTHING).'
    Write-Info 'Para actualizar un curso que ya está allí, usa su manifest desde /admin/importar.'
}

$contentTarget = if ($ContentRoot) { $ContentRoot } else { Join-Path $Root 'content/dist' }
$doContent = (-not $NoContent) -and (Test-Path $zipPath)

if (-not $NoContent -and -not (Test-Path $zipPath)) {
    Write-Warn 'El paquete no trae content.zip. Las lecciones darán 404 salvo que el contenido ya esté en el destino.'
}

if ($doContent) {
    Write-Host ''
    Write-Info "Contenido -> $contentTarget"
    Write-Info "$($manifest.contentFiles) ficheros. Se sobrescriben los que coincidan en nombre."
}

if ($DryRun) {
    Write-Step 'DryRun: no se ha tocado nada'
    Write-Host ''
    exit 0
}

# ── confirmación ─────────────────────────────────────────────────────────────────────────

if (-not $Yes) {
    Write-Host ''
    $answer = Read-Host "Importar en $targetLabel [s/N]"
    if ($answer -notmatch '^[sSyY]') {
        Write-Info 'Cancelado. No se ha tocado nada.'
        exit 0
    }
}

# ── contenido ────────────────────────────────────────────────────────────────────────────

if ($doContent) {
    Write-Step 'Descomprimiendo el contenido'
    New-Item -ItemType Directory -Force -Path $contentTarget | Out-Null
    Expand-Archive -Path $zipPath -DestinationPath $contentTarget -Force
    $written = (Get-ChildItem $contentTarget -Recurse -File).Count
    Write-Ok "$contentTarget ($written ficheros en total)"
}

# ── catálogo ─────────────────────────────────────────────────────────────────────────────
#
# `ON_ERROR_STOP=1` es lo que convierte el BEGIN/COMMIT del paquete en una garantía: sin él
# psql seguiría tras el primer error y llegaría al COMMIT con media importación dentro.

Write-Step 'Aplicando catalog.sql'

if ($useDocker) {
    $remote = '/tmp/inkoova-catalog.sql'

    # La salida de docker se conserva y se enseña: `docker cp` falla también cuando la carpeta
    # del paquete está fuera de las rutas que Docker Desktop tiene compartidas, y ahí el
    # mensaje útil es el suyo («access denied»), no un «no se pudo copiar» a secas.
    $cp = docker cp $sqlPath "${Container}:$remote" 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw @"
No se pudo copiar catalog.sql al contenedor: $cp

Si dice «access denied», el paquete está en una carpeta que Docker Desktop no comparte.
Muévelo dentro del repositorio o de tu carpeta de usuario y repite.
"@
    }

    try {
        docker exec $Container psql -U $User -d $Database -q -v ON_ERROR_STOP=1 -f $remote
        $applied = $LASTEXITCODE
    } finally {
        docker exec $Container rm -f $remote | Out-Null
    }
} else {
    psql -h $PgHost -p $PgPort -U $User -d $Database -q -v ON_ERROR_STOP=1 -f $sqlPath
    $applied = $LASTEXITCODE
}

if ($applied -ne 0) {
    throw 'La importación falló. Por el BEGIN/COMMIT del paquete, la base queda como estaba.'
}

Write-Ok 'Aplicado en una transacción.'

# ── comprobación ─────────────────────────────────────────────────────────────────────────
#
# Se compara contra el manifest, no contra la impresión de que ha ido bien. El destino puede
# tener MÁS filas de las que trae el paquete —si ya había catálogo propio— y eso está bien;
# lo que no puede es tener menos.

Write-Step 'Comprobando'

$after = Get-Counts
$short = @()

foreach ($entry in $expected.GetEnumerator()) {
    $now = $after[$entry.Key]
    $delta = $now - $before[$entry.Key]

    if ($now -lt $entry.Value) {
        $short += $entry.Key
        Write-Bad ("{0,-20} {1,6} de {2,-6} faltan {3}" -f $entry.Key, $now, $entry.Value, ($entry.Value - $now))
    } else {
        $sign = if ($delta -gt 0) { "+$delta" } else { 'sin cambios' }
        Write-Info ("{0,-20} {1,6}  ({2})" -f $entry.Key, $now, $sign)
    }
}

Write-Host ''

if ($short.Count -gt 0) {
    Write-Bad "Faltan filas en: $($short -join ', ')."
    Write-Info 'El SQL se aplicó sin error, así que lo que falta chocó con un ON CONFLICT:'
    Write-Info 'el destino ya tenía filas con ese mismo slug o id y se conservaron las suyas.'
    Write-Info 'Compara a mano antes de dar el traslado por bueno.'
    Write-Host ''
    exit 1
}

Write-Ok 'Todas las tablas tienen al menos las filas del paquete.'

# ── lo que hay que rematar a mano ────────────────────────────────────────────────────────

Write-Step 'Repasa en el destino'
Write-Info 'Stripe: los price id del paquete son de la cuenta de origen. Si la cuenta es otra,'
Write-Info '        rehazlos desde el panel (productos, planes, paquetes de tutoría, descuentos).'

if (-not $manifest.includesSecrets) {
    Write-Info 'Correo: la contraseña SMTP viene vacía. Ponla desde el panel de identidades.'
}

Write-Info 'Fiscal: la numeración de facturas y la cadena Veri*factu empiezan de cero aquí.'
Write-Info 'Acceso: no hay usuarios. Los administradores salen de INKOOVA_OWNER_EMAILS.'
Write-Host ''
