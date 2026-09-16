<#
.SYNOPSIS
    Exporta el catálogo y la configuración de Inkoova Academy. Sin usuarios.

.DESCRIPTION
    Genera un paquete que reconstruye la plataforma en otra base de datos: cursos, temario,
    lecciones, cuestionarios, packs, programa, planes, precios, plantillas, identidades y
    ajustes. Lo que NO viaja es la gente: ni cuentas, ni progreso, ni compras, ni
    suscripciones, ni certificados emitidos, ni facturas, ni la cadena Veri*factu.

    El paquete lleva tres piezas:

      catalog.sql    Las filas, en orden de claves ajenas, en una transacción, con
                     ON CONFLICT DO NOTHING.
      content.zip    Los ficheros que sirven las lecciones y los packs. Sin ellos el temario
                     aparece entero y cada lección devuelve 404: `lesson.content_ref` es una
                     ruta dentro del volumen privado, no el HTML.
      manifest.json  Qué se exportó y cuántas filas de cada tabla. Lo lee `import-catalog.ps1`
                     para comprobar que al otro lado llegó todo.

    El reparto entre lo que viaja y lo que no está escrito tabla a tabla más abajo. Si una
    migración futura añade una tabla que no esté en ninguna de las dos listas, el script para
    y lo dice: es preferible a que un traslado se deje algo en silencio.

.PARAMETER Destination
    Carpeta destino. Por defecto `export/catalog-<fecha>` en la raíz del repositorio.
    Se llama así y no `-Out` porque PowerShell no sabría si `-Out` es esto, `-OutVariable`
    o `-OutBuffer`, y aborta antes de llamar al script.

.PARAMETER IncludeSecrets
    Exporta `academy_identity.smtp_password` en claro en vez de dejarla vacía.

.PARAMETER NoContent
    Salta el zip de contenido. Útil si el destino ya tiene el volumen montado.

.PARAMETER Container
    Nombre o id del contenedor de Postgres de ORIGEN. Por defecto se descubre con compose.

.EXAMPLE
    .\infra\export-catalog.ps1

.EXAMPLE
    .\infra\export-catalog.ps1 -Destination C:\traslado -NoContent
#>

[CmdletBinding()]
param(
    [string]$Destination,
    [switch]$IncludeSecrets,
    [switch]$NoContent,
    [string]$Container,
    [string]$Database = 'academy',
    [string]$User = 'academy'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Root = Split-Path $PSScriptRoot -Parent
$Compose = Join-Path $Root 'infra/docker-compose.yml'
$ContentDist = Join-Path $Root 'content/dist'

function Write-Step($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }
function Write-Ok($m)   { Write-Host "  OK  $m" -ForegroundColor Green }
function Write-Warn($m) { Write-Host "  !   $m" -ForegroundColor Yellow }
function Write-Info($m) { Write-Host "      $m" -ForegroundColor DarkGray }

# ─────────────────────────────────────────────────────────────────────────────────────────
# Lo que viaja.
#
# El orden es el de las claves ajenas, no el alfabético de pg_dump: así el fichero se aplica
# de una pasada sin desactivar disparadores, que exigiría ser superusuario en el destino.
#
# `Blank` son columnas que se vacían al exportar, cada una con el valor con el que se sustituye.
# Es una expresión SQL y no un NULL a secas porque `smtp_password` está declarada NOT NULL:
# vaciarla a NULL haría fallar la importación en el destino, no aquí, que es el peor sitio
# donde enterarse. Dos motivos para vaciar, ninguno estético:
#
#   · `updated_by` y `affiliate_id` apuntan a un usuario o a un afiliado de ESTA base. Como
#     los usuarios no viajan, la fila llegaría al destino apuntando a un id que allí no
#     existe y la clave ajena la rechazaría. El dato que se pierde es «quién tocó esto por
#     última vez aquí», que en la base nueva no significa nada.
#
#   · `smtp_password` es una credencial. Un paquete de traslado se copia, se comprime, se
#     manda por correo y acaba en un disco que no controlas. Sale vacía salvo que se pida lo
#     contrario con -IncludeSecrets, y entonces se avisa por pantalla.
# ─────────────────────────────────────────────────────────────────────────────────────────

# Asignado en dos pasos y no con un `if` en línea: una tabla vacía devuelta desde un bloque de
# script se colapsa a $null al salir de la tubería, y la lista de columnas a vaciar dejaría de
# existir justo en el caso en el que se piden los secretos.
$secretColumns = @{}
if (-not $IncludeSecrets) { $secretColumns = @{ smtp_password = "''" } }

$Tables = @(
    @{ Name = 'academy_identity';  Blank = $secretColumns }
    @{ Name = 'academy_setting';   Blank = @{ updated_by = 'NULL' } }
    @{ Name = 'product';           Blank = @{} }
    @{ Name = 'plan';              Blank = @{} }
    @{ Name = 'plan_product';      Blank = @{} }
    @{ Name = 'course';            Blank = @{} }
    @{ Name = 'section';           Blank = @{} }
    @{ Name = 'lesson';            Blank = @{} }
    @{ Name = 'quiz';              Blank = @{} }
    @{ Name = 'quiz_question';     Blank = @{} }
    @{ Name = 'pack';              Blank = @{} }
    @{ Name = 'pack_file';         Blank = @{} }
    @{ Name = 'learning_program';  Blank = @{} }
    @{ Name = 'program_item';      Blank = @{} }
    @{ Name = 'roadmap_node';      Blank = @{} }
    @{ Name = 'community_session'; Blank = @{} }
    @{ Name = 'tutoring_package';  Blank = @{} }
    @{ Name = 'email_template';    Blank = @{ updated_by = 'NULL' } }
    @{ Name = 'certificate_style'; Blank = @{ updated_by = 'NULL' } }
    @{ Name = 'discount_code';     Blank = @{ affiliate_id = 'NULL' } }
)

# ─────────────────────────────────────────────────────────────────────────────────────────
# Lo que no viaja, y por qué. Está enumerado —en vez de «todo lo demás»— para que el guardián
# de más abajo pueda distinguir una tabla excluida a propósito de una tabla nueva que nadie
# ha clasificado todavía.
# ─────────────────────────────────────────────────────────────────────────────────────────

$Excluded = [ordered]@{
    # Las cuentas y su sesión.
    'identity_user'          = 'Cuentas.'
    'identity_user_role'     = 'Cuentas.'
    'identity_user_token'    = 'Cuentas.'
    'identity_refresh_token' = 'Cuentas.'
    'identity_user_login'    = 'Cuentas.'
    'external_login'         = 'Cuentas.'
    'app_user'               = 'Cuentas.'
    'discord_link'           = 'Cuenta de Discord de cada alumno.'

    # Lo que un alumno ha comprado y lo que le da acceso.
    'purchase'               = 'Compras.'
    'subscription'           = 'Suscripciones.'
    'entitlement'            = 'Derechos de acceso. Se regeneran de las compras y del plan.'

    # Lo que un alumno ha hecho.
    'lesson_progress'        = 'Progreso.'
    'quiz_attempt'           = 'Intentos de cuestionario.'
    'certificate'            = 'Certificados emitidos.'
    'download_log'           = 'Descargas.'
    'consent_record'         = 'Consentimientos. Son de la persona y del sitio donde los dio.'
    'waitlist'               = 'Correos en lista de espera.'

    # Afiliación: cada fila cuelga de una persona.
    'affiliate'              = 'Afiliados.'
    'referral'               = 'Referidos.'
    'payout'                 = 'Liquidaciones.'
    'commission'             = 'Comisiones.'

    # Tutoría: paquetes sí (son catálogo), lo vendido y lo impartido no.
    'tutor'                  = 'Tutores: cada uno es una cuenta.'
    'tutor_earning'          = 'Ganancias de tutor.'
    'tutoring_grant'         = 'Bolsas de minutos compradas.'
    'tutoring_session'       = 'Sesiones impartidas.'
    'tutoring_appointment'   = 'Citas.'

    # Registro fiscal y operativo. Encadenado y con fecha: no se clona, se empieza de cero.
    'fiscal_invoice'         = 'Facturas emitidas. Cadena de hashes propia de esta instalación.'
    'verifactu_record'       = 'Registro Veri*factu. Encadenado; clonarlo rompería la cadena.'
    'verifactu_flow'         = 'Estado del flujo Veri*factu.'
    'stripe_event'           = 'Webhooks ya procesados. Copiarlos haría que el destino ignore esos eventos.'
    'audit_log'              = 'Auditoría de esta instalación.'

    # Del migrador.
    'schemaversions'         = 'Control de DbUp. El destino lleva el suyo.'
}

# ── contenedor ───────────────────────────────────────────────────────────────────────────

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
    throw 'No se encuentra el contenedor de Postgres. Arranca el entorno (.\dev.ps1 up) o pasa -Container.'
}

Write-Step "Exportando desde la base '$Database'"

if ($IncludeSecrets) {
    Write-Warn 'El paquete llevará la contraseña SMTP en claro dentro de catalog.sql.'
    Write-Warn 'Trátalo como un secreto: no lo subas al repositorio ni lo mandes por correo.'
}

# ── guardián: ninguna tabla sin clasificar ───────────────────────────────────────────────
#
# Una tabla nueva que no esté ni incluida ni excluida no se cae del paquete en silencio.
# Aquí se para el traslado y se dice cuál es, para que alguien decida si es catálogo o es
# de alumnos. Es la única forma de que este script envejezca bien.

$known = @($Tables.Name) + @($Excluded.Keys)
$actual = docker exec $Container psql -U $User -d $Database -tA -c @"
SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public' AND c.relkind = 'r' ORDER BY 1
"@

if ($LASTEXITCODE -ne 0) {
    throw "No se puede leer el esquema de la base '$Database'."
}

$actual = @($actual | Where-Object { $_ -and $_.Trim() } | ForEach-Object { $_.Trim() })
$unknown = @($actual | Where-Object { $known -notcontains $_ })

if ($unknown.Count -gt 0) {
    throw @"
Hay tablas que este script no sabe clasificar: $($unknown -join ', ').

Alguien las ha añadido en una migración posterior. Decide si son catálogo o son datos de
alumnos y añádelas a `$Tables o a `$Excluded en infra/export-catalog.ps1. No se exporta
nada hasta entonces: un traslado incompleto y silencioso es peor que uno que no arranca.
"@
}

$missing = @($Tables.Name | Where-Object { $actual -notcontains $_ })
if ($missing.Count -gt 0) {
    throw "La base '$Database' no tiene estas tablas: $($missing -join ', '). ¿Están aplicadas todas las migraciones?"
}

# ── destino ──────────────────────────────────────────────────────────────────────────────

if (-not $Destination) {
    $Destination = Join-Path $Root ('export/catalog-' + (Get-Date -Format 'yyyyMMdd-HHmm'))
}

New-Item -ItemType Directory -Force -Path $Destination | Out-Null
$SqlPath = Join-Path $Destination 'catalog.sql'

# ── recuento previo ──────────────────────────────────────────────────────────────────────

$countQuery = ($Tables | ForEach-Object { "SELECT '$($_.Name)' AS t, count(*) AS n FROM $($_.Name)" }) -join ' UNION ALL '
$countRaw = docker exec $Container psql -U $User -d $Database -tA -F'|' -c $countQuery
if ($LASTEXITCODE -ne 0) {
    throw 'No se pueden contar las filas de origen.'
}

$counts = [ordered]@{}
foreach ($line in $countRaw) {
    if (-not $line -or -not $line.Trim()) { continue }
    $parts = $line.Trim() -split '\|'
    $counts[$parts[0]] = [int]$parts[1]
}

# ── volcado ──────────────────────────────────────────────────────────────────────────────
#
# Las tablas con columnas que vaciar no se vuelcan directamente: se copian antes a un esquema
# temporal, se anulan allí y se vuelca la copia. Así el escapado de literales lo sigue
# haciendo pg_dump y no una plantilla escrita a mano, que es donde aparecen las comillas
# sueltas y los acentos rotos.
#
# `sed -n '/^INSERT INTO/,$p'` recorta el preámbulo de SET que pg_dump repite en cada tabla.
# Es un rango desde la primera coincidencia hasta el final, no un filtro línea a línea: un
# valor de texto multilínea (una descripción larga, un `options_json`) sale intacto aunque
# alguna de sus líneas empiece por `--` o esté en blanco.
#
# El segundo `sed` quita el `\unrestrict` de cierre. Desde 17.6 pg_dump envuelve el volcado
# entre dos metacomandos de psql, y el de apertura se va con el preámbulo: dejar solo el de
# cierre aborta la importación con «not currently in restricted mode».

$stage = 'inkoova_export'
$remoteBody = '/tmp/inkoova-catalog-body.sql'
$remoteStage = '/tmp/inkoova-stage.sql'
$remoteScript = '/tmp/inkoova-dump.sh'

$sanitized = @($Tables | Where-Object { $_.Blank.Count -gt 0 })

$stageSql = New-Object System.Text.StringBuilder
# El esquema temporal se crea y se tira en cada exportación; los avisos de «no existía» y de
# «se borran en cascada» son ruido que haría dudar de una ejecución correcta.
[void]$stageSql.AppendLine('SET client_min_messages = warning;')
[void]$stageSql.AppendLine("DROP SCHEMA IF EXISTS $stage CASCADE;")
[void]$stageSql.AppendLine("CREATE SCHEMA $stage;")
foreach ($table in $sanitized) {
    [void]$stageSql.AppendLine("CREATE TABLE $stage.$($table.Name) AS TABLE public.$($table.Name);")
    foreach ($column in $table.Blank.GetEnumerator()) {
        [void]$stageSql.AppendLine("UPDATE $stage.$($table.Name) SET $($column.Key) = $($column.Value);")
    }
}

$dumpFlags = '--data-only --column-inserts --on-conflict-do-nothing --no-owner --no-privileges'

$sh = New-Object System.Text.StringBuilder
[void]$sh.AppendLine('set -e')
[void]$sh.AppendLine("rm -f $remoteBody")
if ($sanitized.Count -gt 0) {
    [void]$sh.AppendLine("psql -U $User -d $Database -q -v ON_ERROR_STOP=1 -f $remoteStage")
}
foreach ($table in $Tables) {
    $name = $table.Name
    [void]$sh.AppendLine("echo '-- tabla: $name' >> $remoteBody")

    if ($table.Blank.Count -gt 0) {
        # El `sed` de reescritura va anclado a `^INSERT INTO ` para no tocar nada que pudiera
        # parecerse dentro de un valor de texto.
        [void]$sh.AppendLine("pg_dump -U $User -d $Database $dumpFlags -t '$stage.$name' | sed -n '/^INSERT INTO/,`$p' | sed '/^\\unrestrict /d' | sed 's/^INSERT INTO $stage\./INSERT INTO public./' >> $remoteBody")
    } else {
        [void]$sh.AppendLine("pg_dump -U $User -d $Database $dumpFlags -t 'public.$name' | sed -n '/^INSERT INTO/,`$p' | sed '/^\\unrestrict /d' >> $remoteBody")
    }

    [void]$sh.AppendLine("echo '' >> $remoteBody")
}
if ($sanitized.Count -gt 0) {
    [void]$sh.AppendLine("psql -U $User -d $Database -q -c 'SET client_min_messages = warning; DROP SCHEMA IF EXISTS $stage CASCADE;'")
}

# Los guiones se mantienen en ASCII y entran por `docker cp`, no por la tubería: PowerShell
# reescribe con CRLF todo lo que manda por stdin a un proceso nativo, y `done\r` no es `done`
# para el intérprete de Alpine.
$utf8 = [System.Text.UTF8Encoding]::new($false)
$tmp = [System.IO.Path]::GetTempPath()
$localScript = Join-Path $tmp ('inkoova-dump-' + [guid]::NewGuid().ToString('N') + '.sh')
$localStage = Join-Path $tmp ('inkoova-stage-' + [guid]::NewGuid().ToString('N') + '.sql')

[System.IO.File]::WriteAllText($localScript, ($sh.ToString() -replace "`r`n", "`n"), $utf8)
[System.IO.File]::WriteAllText($localStage, ($stageSql.ToString() -replace "`r`n", "`n"), $utf8)

try {
    $cp = @()
    $cp += docker cp $localScript "${Container}:$remoteScript" 2>&1
    $cp += docker cp $localStage "${Container}:$remoteStage" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "No se pudo copiar el guion de volcado al contenedor: $cp" }

    docker exec $Container sh $remoteScript
    if ($LASTEXITCODE -ne 0) { throw 'pg_dump falló dentro del contenedor.' }
} finally {
    # `Remove-Item` comprueba antes si la ruta es un enlace simbólico, y esa comprobación
    # lanza «Acceso denegado» en algunos temporales de Windows. El borrado de .NET no mira.
    try { [System.IO.File]::Delete($localScript) } catch { }
    try { [System.IO.File]::Delete($localStage) } catch { }
    docker exec $Container rm -f $remoteScript $remoteStage | Out-Null
}

$bodyPath = Join-Path $Destination '.body.sql'
$cpOut = docker cp "${Container}:$remoteBody" $bodyPath 2>&1
if ($LASTEXITCODE -ne 0) {
    throw @"
No se pudo copiar el volcado fuera del contenedor: $cpOut

Si dice «access denied», la carpeta de -Destination está fuera de las rutas que Docker Desktop
comparte. Elige una dentro del repositorio o de tu carpeta de usuario.
"@
}
docker exec $Container rm -f $remoteBody | Out-Null

$body = [System.IO.File]::ReadAllText($bodyPath, $utf8)
[System.IO.File]::Delete($bodyPath)

foreach ($table in $Tables) {
    Write-Info ("{0,-20} {1,6}" -f $table.Name, $counts[$table.Name])
}

$stamp = Get-Date -Format 'yyyy-MM-dd HH:mm'

$header = @"
-- Catálogo y configuración de Inkoova Academy.
-- Exportado el $stamp desde la base '$Database'.
--
-- Contiene: cursos, temario, lecciones, cuestionarios, packs, programa, planes, precios,
-- plantillas de correo y certificado, identidades y ajustes.
-- NO contiene nada de personas: ni cuentas, ni progreso, ni compras, ni suscripciones, ni
-- certificados emitidos, ni facturas, ni la cadena Veri*factu.
--
-- Requisito del destino: el esquema ya creado y al día. Ejecuta antes el migrador (DbUp),
-- o los INSERT fallarán por tablas o columnas que todavía no existen.
--
-- Aplicar con el importador, que además comprueba las cifras:
--   .\infra\import-catalog.ps1 -Package <esta carpeta>
--
-- O a mano:
--   psql -U <usuario> -d <base> -v ON_ERROR_STOP=1 -f catalog.sql
--
-- Es idempotente por ON CONFLICT DO NOTHING: repetirlo no duplica filas. Tampoco ACTUALIZA
-- las que ya estén; para refrescar un curso ya importado está el manifest.

SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;

BEGIN;

"@

[System.IO.File]::WriteAllText($SqlPath, $header + $body + "`nCOMMIT;`n", $utf8)
Write-Ok ("catalog.sql ({0} KB)" -f [math]::Round((Get-Item $SqlPath).Length / 1KB))

# ── contenido ────────────────────────────────────────────────────────────────────────────

$contentFiles = 0
if (-not $NoContent) {
    Write-Step 'Empaquetando el contenido'

    if (-not (Test-Path $ContentDist)) {
        Write-Warn "No existe $ContentDist. El paquete va sin ficheros: en el destino las lecciones darían 404."
    } else {
        $staging = Join-Path $tmp ('inkoova-content-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Force -Path $staging | Out-Null

        try {
            # `certificates/` son PDF emitidos a alumnos concretos de ESTA instalación. No son
            # catálogo y no viajan.
            Get-ChildItem -Path $ContentDist -Directory |
                Where-Object { $_.Name -ne 'certificates' } |
                ForEach-Object { Copy-Item $_.FullName -Destination $staging -Recurse }

            $zip = Join-Path $Destination 'content.zip'
            if (Test-Path $zip) { Remove-Item $zip -Force }
            Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip -CompressionLevel Optimal

            $contentFiles = (Get-ChildItem $staging -Recurse -File).Count
            Write-Ok ("content.zip ({0} ficheros, {1} MB)" -f $contentFiles, [math]::Round((Get-Item $zip).Length / 1MB, 1))
        } finally {
            try { [System.IO.Directory]::Delete($staging, $true) } catch { }
        }
    }
}

# ── manifests de curso ───────────────────────────────────────────────────────────────────
#
# Van de propina porque son la vía que SÍ converge: aplicar un manifest desde /admin/importar
# actualiza un curso que ya existe, cosa que el SQL con ON CONFLICT DO NOTHING no hace.

$manifests = Join-Path $Root 'content/manifests'
$manifestCount = 0
if (Test-Path $manifests) {
    $dest = Join-Path $Destination 'manifests'
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item (Join-Path $manifests '*.json') -Destination $dest -Force
    $manifestCount = (Get-ChildItem $dest -File).Count
    Write-Ok "manifests ($manifestCount ficheros)"
}

# ── manifest del paquete ─────────────────────────────────────────────────────────────────
#
# Lo lee el importador para comprobar, tabla a tabla, que al otro lado llegó lo mismo que
# salió de aquí. Sin esto «ha ido bien» es una impresión y no una comprobación.

$manifest = [ordered]@{
    exportedAt     = (Get-Date).ToString('o')
    sourceDatabase = $Database
    includesUsers  = $false
    includesSecrets = [bool]$IncludeSecrets
    contentFiles   = $contentFiles
    courseManifests = $manifestCount
    tables         = $counts
    excluded       = $Excluded
}

[System.IO.File]::WriteAllText(
    (Join-Path $Destination 'manifest.json'),
    ($manifest | ConvertTo-Json -Depth 5),
    $utf8)

# ── instrucciones ────────────────────────────────────────────────────────────────────────

$countRows = ($counts.GetEnumerator() | ForEach-Object { "| ``$($_.Key)`` | $($_.Value) |" }) -join "`n"
$excludedRows = ($Excluded.GetEnumerator() | ForEach-Object { "| ``$($_.Key)`` | $($_.Value) |" }) -join "`n"

$secretNote = if ($IncludeSecrets) {
    '> **Este paquete lleva la contraseña SMTP en claro.** Trátalo como un secreto: no lo subas
> al repositorio, no lo mandes por correo y bórralo del disco cuando termines el traslado.'
} else {
    'La contraseña SMTP de `academy_identity` sale vacía. Ponla en el destino desde el panel,
o reexporta con `-IncludeSecrets` si prefieres que viaje (y entonces trata el paquete como un
secreto).'
}

$readme = @"
# Traslado de Inkoova Academy · catálogo y configuración

Exportado el $stamp desde la base ``$Database``.

## Qué hay aquí

| Fichero | Qué es |
|---|---|
| ``catalog.sql`` | Las filas, en orden de claves ajenas, en una transacción. |
| ``content.zip`` | Los ficheros que sirven las lecciones y los packs ($contentFiles ficheros). |
| ``manifest.json`` | Qué se exportó y cuántas filas. Lo comprueba el importador. |
| ``manifests/`` | Los manifests de curso ($manifestCount), por si se prefiere importar por ahí. |

## Cómo importarlo

1. **El esquema primero**, en el destino:

   ``````
   dotnet run --project api/src/Inkoova.Academy.Migrator
   ``````

   Este paquete lleva datos, no estructura.

2. **Todo lo demás**, con el importador:

   ``````
   .\infra\import-catalog.ps1 -Package <esta carpeta>
   ``````

   Descomprime el contenido en la raíz configurada, aplica el SQL y compara las cifras
   contra ``manifest.json``. Con ``-DryRun`` dice qué haría sin tocar nada.

   A mano, si no hay PowerShell en el destino:

   ``````
   unzip content.zip -d <Academy__ContentRoot>
   psql -U <usuario> -d <base> -v ON_ERROR_STOP=1 -f catalog.sql
   ``````

## Qué viaja

| Tabla | Filas |
|---|---|
$countRows

## Qué NO viaja, y por qué

| Tabla | Motivo |
|---|---|
$excludedRows

La consecuencia práctica: el destino arranca con el catálogo completo y sin un solo alumno.
Los administradores se crean por configuración (``INKOOVA_OWNER_EMAILS``), no por base de
datos, así que no hace falta traer ninguna cuenta para poder entrar al panel.

## Cosas que repasar en el destino

- **Stripe.** ``product.stripe_price_id``, ``plan.stripe_price_id``,
  ``tutoring_package.stripe_price_id`` y ``discount_code.stripe_promotion_code_id`` son de la
  cuenta de Stripe de origen. Si el destino usa otra cuenta, allí no existen y hay que
  rehacerlos desde el panel.
- **Correo.** $secretNote
- **Identidad fiscal.** ``academy_identity`` viaja con NIF, domicilio y series. Revisa que es
  la que corresponde a la instalación nueva antes de emitir nada.
- **Facturación.** La numeración y la cadena Veri*factu empiezan de cero en el destino: no se
  clonan a propósito.

## Reimportar encima

``catalog.sql`` usa ``ON CONFLICT DO NOTHING``: aplicarlo dos veces no duplica, pero tampoco
actualiza lo que ya esté. Para refrescar un curso que ya existe en el destino, aplica su
manifest desde ``/admin/importar``: esa vía sí converge y preserva los ids de lección por
slug, así que el progreso de los alumnos sobrevive.
"@

[System.IO.File]::WriteAllText((Join-Path $Destination 'README.md'), $readme, $utf8)

Write-Step 'Listo'
Write-Info $Destination
Write-Host ''
Write-Info ("{0} tablas, {1} filas en total" -f $counts.Count, (($counts.Values | Measure-Object -Sum).Sum))
Write-Info 'Instrucciones en README.md. Para importarlo: .\infra\import-catalog.ps1 -Package <carpeta>'
Write-Host ''
