<#
.SYNOPSIS
    Sube el contenido de los cursos (content/dist) al share de Azure Files.

.DESCRIPTION
    El share 'content' es lo que las Container Apps montan como /srv/content: la API y los
    jobs en lectura-escritura, Caddy en solo lectura. Este script lo rellena desde la copia
    local con `az storage file upload-batch`.

    Es idempotente: subir dos veces deja el share igual que subir una. Los ficheros que ya
    existan con el mismo nombre se sobrescriben; los que solo estén en el share no se
    borran (igual que la descompresión de import-catalog.ps1). Para retirar un curso,
    bórralo a mano en el share o recrea el share.

    La autenticación sale de `az login`: con la sesión de Azure se pide la clave de la
    cuenta de almacenamiento y con ella se sube. No hay que copiar claves a mano.

.PARAMETER StorageAccount
    Nombre de la cuenta de almacenamiento (salida `storage_account_name` de terraform).

.PARAMETER Share
    Nombre del share. Por defecto 'content', que es el que crea terraform.

.PARAMETER Source
    Carpeta local a subir. Por defecto `content/dist` del repositorio, la misma que
    docker-compose monta en /srv/content.

.PARAMETER DryRun
    Enseña qué subiría y no sube nada.

.EXAMPLE
    .\infra\azure\sync-content.ps1 -StorageAccount stacademyprodx1y2 -DryRun

.EXAMPLE
    .\infra\azure\sync-content.ps1 -StorageAccount stacademyprodx1y2
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$StorageAccount,

    [string]$Share = 'content',
    [string]$Source,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent

function Write-Step($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }
function Write-Ok($m)   { Write-Host "  OK  $m" -ForegroundColor Green }
function Write-Info($m) { Write-Host "      $m" -ForegroundColor DarkGray }

# ── requisitos ───────────────────────────────────────────────────────────────────────────

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Hace falta el Azure CLI (az). Instálalo y haz `az login` antes de lanzar esto.'
}

$account = az account show --query name -o tsv 2>$null
if ($LASTEXITCODE -ne 0 -or -not $account) {
    throw 'No hay sesión de Azure. Haz `az login` (y `az account set` si tienes varias suscripciones).'
}

if (-not $Source) {
    $Source = Join-Path $Root 'content/dist'
}

if (-not (Test-Path $Source)) {
    throw "No existe la carpeta de origen: $Source. ¿Está el contenido descomprimido? (import-catalog.ps1 lo deja ahí)."
}

$files = Get-ChildItem $Source -Recurse -File
if ($files.Count -eq 0) {
    throw "La carpeta $Source está vacía: no hay nada que subir."
}

$totalMb = [math]::Round(($files | Measure-Object Length -Sum).Sum / 1MB, 1)

Write-Step 'Qué se va a subir'
Write-Info "Origen   $Source"
Write-Info "Destino  cuenta '$StorageAccount' · share '$Share' (sesión: $account)"
Write-Info ("Volumen  {0} ficheros, {1} MB" -f $files.Count, $totalMb)
Write-Info 'Los ficheros con el mismo nombre se sobrescriben; nada se borra del share.'

if ($DryRun) {
    Write-Step 'DryRun: no se ha subido nada'
    Write-Host ''
    exit 0
}

# ── clave de la cuenta ───────────────────────────────────────────────────────────────────
# Se pide con la sesión de az y se usa solo en memoria. Es más fiable que --auth-mode
# login, que para Azure Files exige un rol RBAC de datos que la suscripción normal no
# reparte por defecto.

Write-Step 'Obteniendo la clave de la cuenta'

$key = az storage account keys list --account-name $StorageAccount --query '[0].value' -o tsv
if ($LASTEXITCODE -ne 0 -or -not $key) {
    throw "No se pudo leer la clave de '$StorageAccount'. ¿Existe la cuenta y tu sesión tiene permiso sobre su resource group?"
}

Write-Ok 'Clave obtenida (no se guarda en ningún sitio).'

# ── subida ───────────────────────────────────────────────────────────────────────────────
# upload-batch recorre la carpeta y respeta la estructura de subcarpetas: el share queda
# calcado a content/dist, que es exactamente lo que la API espera en /srv/content.

Write-Step "Subiendo a '$Share'"

az storage file upload-batch `
    --account-name $StorageAccount `
    --account-key $key `
    --destination $Share `
    --source $Source `
    --no-progress `
    --output none

if ($LASTEXITCODE -ne 0) {
    throw 'La subida falló. La operación es re-lanzable: corrige y repite, no queda nada a medias que estorbe.'
}

Write-Ok ("Subidos {0} ficheros ({1} MB)." -f $files.Count, $totalMb)
Write-Info 'Las apps ven los cambios al momento: /srv/content es el share, no una copia.'
Write-Host ''
