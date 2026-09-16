<#
.SYNOPSIS
    Traslada la base de datos LOCAL completa al PostgreSQL Flexible Server de Azure.

.DESCRIPTION
    Dos pasos, ambos dentro de Docker para no exigir herramientas de Postgres instaladas:

      1. pg_dump en formato custom DENTRO del contenedor local de Postgres (docker exec).
         El volcado se copia a disco y se conserva: es también tu copia de seguridad del
         momento del traslado.
      2. pg_restore contra Azure desde un contenedor efímero postgres:17-alpine
         (docker run), con --no-owner --no-privileges porque el usuario administrador del
         Flexible Server no es superusuario y los OWNER/GRANT del origen no aplican allí.

    ATENCIÓN: la restauración lleva --clean --if-exists, es decir, MACHACA en el destino
    todos los objetos que vengan en el volcado. Los datos que hubiera en la base de Azure
    se pierden. Es lo que se quiere en una migración inicial; no lo lances contra un
    entorno con datos buenos sin pensarlo dos veces.

    El esquema lo crean las migraciones de DbUp, pero aquí no hace falta lanzarlas antes:
    el volcado lleva estructura y datos (incluida la tabla de control de DbUp), así que el
    siguiente arranque del migrator no re-aplica nada.

.PARAMETER AzureHost
    FQDN del Flexible Server (salida `postgres_fqdn` de terraform).

.PARAMETER AzureUser
    Usuario administrador del servidor. Por defecto 'academy' (el de terraform).

.PARAMETER Database
    Base de datos de DESTINO en Azure.

.PARAMETER Container
    Contenedor local de Postgres de ORIGEN. Por defecto se descubre con docker compose,
    y si no, por la imagen postgres:17-alpine (en este repo: inkoova-academy-postgres-1).

.PARAMETER LocalDatabase
    Base de datos de ORIGEN en el contenedor local.

.PARAMETER LocalUser
    Usuario del Postgres local.

.PARAMETER DumpPath
    Dónde dejar el volcado. Por defecto, un fichero con fecha junto a este script.

.PARAMETER DryRun
    Hace el volcado y para: no toca Azure. Sirve para comprobar que el dump sale bien.

.PARAMETER Yes
    No pregunta antes de escribir en el destino.

.EXAMPLE
    $env:AZURE_PGPASSWORD = (terraform output -raw postgres_admin_password)
    .\infra\azure\migrate-db.ps1 -AzureHost psql-inkoova-academy-prod.postgres.database.azure.com -DryRun

.EXAMPLE
    .\infra\azure\migrate-db.ps1 -AzureHost psql-inkoova-academy-prod.postgres.database.azure.com

.NOTES
    La contraseña de Azure va en $env:AZURE_PGPASSWORD, nunca en un parámetro: los
    parámetros quedan en el historial de la consola.

    El firewall del Flexible Server tiene que dejar pasar tu IP: rellena developer_ip en
    el tfvars y aplica terraform antes de lanzar esto.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AzureHost,

    [string]$AzureUser = 'academy',
    [string]$Database = 'academy',

    [string]$Container,
    [string]$LocalDatabase = 'academy',
    [string]$LocalUser = 'academy',

    [string]$DumpPath,
    [switch]$DryRun,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$Compose = Join-Path $Root 'infra/docker-compose.yml'

function Write-Step($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }
function Write-Ok($m)   { Write-Host "  OK  $m" -ForegroundColor Green }
function Write-Warn($m) { Write-Host "  !   $m" -ForegroundColor Yellow }
function Write-Info($m) { Write-Host "      $m" -ForegroundColor DarkGray }

# ── requisitos ───────────────────────────────────────────────────────────────────────────

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Hace falta docker: tanto el volcado como la restauración corren en contenedores.'
}

if (-not $DryRun -and -not $env:AZURE_PGPASSWORD) {
    throw @'
Falta la contraseña del servidor de Azure. Ponla en el entorno antes de lanzar:
  $env:AZURE_PGPASSWORD = (terraform output -raw postgres_admin_password)
'@
}

# ── contenedor de origen ─────────────────────────────────────────────────────────────────

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
    throw 'No hay contenedor local de Postgres. Arranca el entorno (dev.ps1) o pasa -Container.'
}

Write-Step "Origen: contenedor $Container · base '$LocalDatabase'"

# ── volcado ──────────────────────────────────────────────────────────────────────────────
# Formato custom (-Fc): comprimido, restaurable por partes, y el único que entiende
# pg_restore. Se genera dentro del contenedor y se copia fuera con docker cp.

if (-not $DumpPath) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $DumpPath = Join-Path $PSScriptRoot "academy-$stamp.dump"
}

Write-Step 'Volcando la base local (pg_dump -Fc)'

$remoteDump = '/tmp/academy-migrate.dump'
docker exec $Container pg_dump -U $LocalUser -d $LocalDatabase -Fc -f $remoteDump
if ($LASTEXITCODE -ne 0) {
    throw 'pg_dump falló dentro del contenedor. Mira su salida más arriba.'
}

try {
    docker cp "${Container}:$remoteDump" $DumpPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "No se pudo copiar el volcado fuera del contenedor."
    }
} finally {
    docker exec $Container rm -f $remoteDump | Out-Null
}

$dumpSize = [math]::Round((Get-Item $DumpPath).Length / 1MB, 1)
Write-Ok "$DumpPath ($dumpSize MB)"

if ($DryRun) {
    Write-Step 'DryRun: Azure no se ha tocado'
    Write-Info 'El volcado queda en disco. Repite sin -DryRun para restaurarlo en Azure.'
    Write-Host ''
    exit 0
}

# ── confirmación ─────────────────────────────────────────────────────────────────────────

Write-Step "Destino: $AzureHost · base '$Database' · usuario '$AzureUser'"
Write-Warn 'La restauración lleva --clean --if-exists: lo que haya en esa base SE PIERDE.'

if (-not $Yes) {
    $answer = Read-Host "¿Machacar '$Database' en $AzureHost con el volcado local? [s/N]"
    if ($answer -notmatch '^[sSyY]') {
        Write-Info 'Cancelado. El volcado queda en disco; Azure no se ha tocado.'
        exit 0
    }
}

# ── restauración ─────────────────────────────────────────────────────────────────────────
# En un contenedor efímero para no exigir pg_restore local. El volcado entra por un bind
# mount de solo lectura; la contraseña y el sslmode (Azure exige TLS) van por el entorno
# del contenedor, no por la línea de comandos.

Write-Step 'Restaurando en Azure (pg_restore --clean --if-exists --no-owner --no-privileges)'
Write-Info 'Directo al puerto 5432, nunca por un pooler: la restauración es DDL puro.'

$dumpDir = Split-Path (Resolve-Path $DumpPath).Path -Parent
$dumpFile = Split-Path $DumpPath -Leaf

docker run --rm `
    -e PGPASSWORD=$env:AZURE_PGPASSWORD `
    -e PGSSLMODE=require `
    -v "${dumpDir}:/dump:ro" `
    postgres:17-alpine `
    pg_restore -h $AzureHost -p 5432 -U $AzureUser -d $Database `
    --clean --if-exists --no-owner --no-privileges `
    "/dump/$dumpFile"

if ($LASTEXITCODE -ne 0) {
    throw @"
pg_restore terminó con errores. Los sospechosos habituales:
  - El firewall del servidor no deja pasar tu IP (developer_ip en el tfvars + terraform apply).
  - AZURE_PGPASSWORD no es la contraseña actual (terraform output -raw postgres_admin_password).
  - La base '$Database' no existe (la crea terraform, no este script).
El volcado sigue en $DumpPath; corrige y repite, la restauración es re-lanzable.
"@
}

Write-Ok 'Restauración completada.'

# ── comprobación rápida ──────────────────────────────────────────────────────────────────
# Un recuento de tablas de usuario basta para distinguir «restauró» de «terminó sin hacer
# nada». La verificación fina (filas por tabla) ya la hace import-catalog.ps1 cuando toca.

Write-Step 'Comprobando'

$tableCount = docker run --rm `
    -e PGPASSWORD=$env:AZURE_PGPASSWORD `
    -e PGSSLMODE=require `
    postgres:17-alpine `
    psql -h $AzureHost -p 5432 -U $AzureUser -d $Database -tA `
    -c "SELECT count(*) FROM pg_tables WHERE schemaname = 'public'"

if ($LASTEXITCODE -ne 0) {
    Write-Warn 'La restauración acabó pero la comprobación no pudo conectar. Revisa a mano.'
} else {
    Write-Ok "El destino tiene $($tableCount.Trim()) tablas en 'public'."
}

Write-Step 'Siguientes pasos'
Write-Info 'Contenido: los ficheros de curso no viajan con la base. Súbelos con sync-content.ps1.'
Write-Info "Volcado:   $DumpPath es tu copia del momento del traslado. Guárdalo o bórralo, pero a sabiendas."
Write-Host ''
