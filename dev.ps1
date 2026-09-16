<#
.SYNOPSIS
    Arranque y verificación de Inkoova Academy en local.

.DESCRIPTION
    Por defecto levanta Postgres en Docker y ejecuta la API y la web de forma nativa. Es el
    bucle de desarrollo rápido: la API recompila en segundos y Vite conserva el HMR, sin
    esperar a que Docker reconstruya imágenes en cada cambio.

    Con -Full levanta todo en Docker Compose, que es lo que se parece a producción.

.PARAMETER Command
    up        Levanta el entorno (contenido + base de datos + API + web).
    down      Para lo que este script haya levantado. Conserva los datos.
    restart   down + up.
    reset     Para todo y BORRA la base de datos. Pide confirmación.
    content   Importa el contenido de los cursos que haya cambiado.
    export    Exporta el catálogo y la configuración (sin usuarios) a un paquete.
    import    Aplica uno de esos paquetes en una base de datos.
    verify    Compila y ejecuta todas las comprobaciones.
    test      Solo los tests.
    logs      Sigue los logs de los contenedores.
    status    Qué está levantado ahora mismo.

.EXAMPLE
    .\dev.ps1 up
    Levanta todo. La web queda en http://localhost:5173 y la API en http://localhost:5080.

.EXAMPLE
    .\dev.ps1 verify
    Compila, pasa tests de dominio, typecheck, lint y build de la web.
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('up', 'down', 'restart', 'reset', 'content', 'export', 'import', 'verify', 'test', 'logs', 'status', 'help')]
    [string]$Command = 'help',

    # Todo en Docker Compose en vez de API y web nativas.
    [switch]$Full,

    # No arranca la web: útil cuando solo se trabaja en la API.
    [switch]$NoWeb,

    # Salta la importación de contenido al arrancar (es lo lento del primer arranque).
    [switch]$SkipContent,

    # Incluye los tests de integración, que necesitan el demonio de Docker.
    [switch]$Integration,

    # Reimporta el contenido aunque no haya cambiado.
    [switch]$Force,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Rest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Root = $PSScriptRoot
$Compose = Join-Path $Root 'infra/docker-compose.yml'
$PidFile = Join-Path $Root '.dev-processes.json'
$ContentDist = Join-Path $Root 'content/dist'

# Raíz de la formación. Se puede sobrescribir con INKOOVA_CONTENT_SOURCE si el repositorio
# vive en otra máquina.
$ContentRoot = if ($env:INKOOVA_CONTENT_SOURCE) {
    $env:INKOOVA_CONTENT_SOURCE
} else {
    Join-Path (Split-Path $Root -Parent) 'Formación'
}

# Los cursos a importar. Uno es una carpeta y otro un único HTML: el importador distingue
# por lo que encuentra en la ruta, no por una bandera.
$Courses = @(
    [pscustomobject]@{
        Slug  = 'agent-engineering-v3'
        Path  = 'curso-autodidacta'
        Title = 'Agent Engineering v3.0'
        # Sin muestras: el único lead magnet son los dos módulos gratis de prompt-engineering.
        # Los valores vacíos pisan los defaults del importador (B0 y P0).
        Extra = @('--free-blocks', '', '--free-precourse-blocks', '')
    },
    [pscustomobject]@{
        Slug  = 'prompt-engineering'
        Path  = 'curso-prompt-engineering.html'
        Title = 'Prompt Engineering Profesional'
        # Solo el módulo 1 gratis como lead magnet.
        Extra = @('--free-modules', '1')
    }
)

# Puerto del Postgres de desarrollo. No es 5432 a propósito: casi todas las máquinas tienen
# ya un Postgres local ahí, y si el bind falla Docker arranca el contenedor igual SIN
# publicar el puerto. La API se conectaría entonces a la base de datos equivocada.
$PostgresPort = 5433

# Administradores globales de la plataforma. Van por configuración y no por base de datos: quien
# manda no debe poder cambiarse desde la propia plataforma ni perderse al restaurar una copia.
$OwnerEmails = if ($env:INKOOVA_OWNER_EMAILS) {
    $env:INKOOVA_OWNER_EMAILS -split ','
} else {
    @('txemabalseiro@hotmail.com','jmgonzalez@inkoova.com','dspuig@inkoova.com')
}

$ApiUrl = 'http://localhost:5080'
$WebUrl = 'http://localhost:5173'

$ConnectionString = "Host=localhost;Port=$PostgresPort;Database=academy;Username=academy;Password=academy"

# ── salida ───────────────────────────────────────────────────────────────────────────────

function Write-Step($message) { Write-Host "`n==> $message" -ForegroundColor Cyan }
function Write-Ok($message) { Write-Host "  OK  $message" -ForegroundColor Green }
function Write-Warn($message) { Write-Host "  !   $message" -ForegroundColor Yellow }
function Write-Bad($message) { Write-Host "  X   $message" -ForegroundColor Red }
function Write-Info($message) { Write-Host "      $message" -ForegroundColor DarkGray }

# ── comprobaciones previas ───────────────────────────────────────────────────────────────

function Test-Tool([string]$name, [string]$versionArgs) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        return $false
    }

    $version = (& $name $versionArgs.Split(' ') 2>&1 | Select-Object -First 1)
    Write-Ok "$name $version"
    return $true
}

function Test-DockerRunning {
    # `docker info` devuelve código distinto de cero cuando el cliente está pero el demonio no.
    docker info 2>&1 | Out-Null
    return $LASTEXITCODE -eq 0
}

function Assert-Prerequisites([switch]$RequireDocker, [switch]$RequireNode, [switch]$RequirePython) {
    Write-Step 'Comprobando herramientas'

    $missing = @()

    if (-not (Test-Tool 'dotnet' '--version')) { $missing += '.NET SDK (https://dotnet.microsoft.com/download)' }
    if ($RequireNode -and -not (Test-Tool 'node' '--version')) { $missing += 'Node.js (https://nodejs.org)' }
    if ($RequirePython -and -not (Test-Tool 'python' '--version')) { $missing += 'Python (https://python.org)' }

    $dockerStopped = $false

    if ($RequireDocker) {
        if (-not (Test-Tool 'docker' '--version')) {
            $missing += 'Docker (https://docs.docker.com/desktop/)'
        } elseif (Test-DockerRunning) {
            Write-Ok 'Docker en marcha'
        } else {
            # Instalado pero parado. Es un caso distinto de "falta instalarlo" y el mensaje
            # debe decir qué hacer: arrancarlo, no instalar nada.
            $dockerStopped = $true
        }
    }

    if ($dockerStopped) {
        throw 'Docker está instalado pero su demonio no responde. Arranca Docker Desktop y repite.'
    }

    if ($missing.Count -gt 0) {
        throw "Falta instalar: $($missing -join ', ')."
    }
}

# ── seguimiento de procesos nativos ──────────────────────────────────────────────────────

# Margen al comparar la hora de arranque de un proceso. Comparar ticks exactos es frágil
# (redondeos al serializar, el valor asentándose justo tras Start-Process) y su único efecto
# sería no matar procesos que sí son nuestros, dejándolos huérfanos. Dos segundos siguen
# descartando un PID reutilizado, que siempre arranca mucho después.
$PidToleranceSeconds = 2

function Test-SameProcess($entry, $process) {
    if (-not $entry.startTime) {
        return $true
    }

    $recorded = [datetime]::MinValue

    if (-not [datetime]::TryParse(
            $entry.startTime,
            [cultureinfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::RoundtripKind,
            [ref]$recorded)) {
        return $true
    }

    return [Math]::Abs(($process.StartTime - $recorded).TotalSeconds) -le $PidToleranceSeconds
}

function Read-TrackedEntries {
    <#
        Lee el fichero de PIDs y devuelve SIEMPRE una lista plana de entradas.

        Windows PowerShell 5.1 y pwsh 7 no se comportan igual: en 5.1 `ConvertFrom-Json`
        devuelve el array JSON entero como UN objeto, sin enumerarlo, así que `@(...)` da
        una lista de un elemento que contiene otra lista. Al añadirle la entrada nueva se
        anida, y la siguiente escritura guarda `{"value":[...],"Count":n}` en vez de las
        entradas. A partir de ahí `down` recorre ese envoltorio, no encuentra `.pid` y no
        para nada: los procesos quedan huérfanos bloqueando los binarios y el puerto.

        Se desenvuelve aquí, y también se acepta un fichero ya corrupto de una versión
        anterior en vez de obligar a borrarlo a mano.
    #>
    if (-not (Test-Path $PidFile)) {
        return , @()
    }

    $raw = Get-Content $PidFile -Raw -ErrorAction SilentlyContinue

    if ([string]::IsNullOrWhiteSpace($raw)) {
        return , @()
    }

    try {
        $parsed = $raw | ConvertFrom-Json
    } catch {
        Write-Warn 'El registro de procesos estaba ilegible; se descarta.'
        return , @()
    }

    $flat = foreach ($item in @($parsed)) {
        if ($null -eq $item) { continue }

        $names = @($item.PSObject.Properties.Name)

        # El envoltorio de 5.1: la lista real viaja dentro de `value`.
        if ($names -contains 'value' -and $names -notcontains 'pid') {
            foreach ($nested in @($item.value)) { $nested }
        } else {
            $item
        }
    }

    return , @($flat | Where-Object { $_ -and @($_.PSObject.Properties.Name) -contains 'pid' })
}

function Save-Process([string]$name, [int]$processId) {
    # Sin `@()` alrededor: la lista ya llega envuelta y volver a envolverla la anidaría otra
    # vez, que es justo el defecto que Read-TrackedEntries existe para no repetir.
    $entries = Read-TrackedEntries

    # Se guarda la hora de arranque junto al PID: Windows reutiliza PIDs, y sin ella `down`
    # podría matar un proceso ajeno que heredó el número.
    $process = Get-Process -Id $processId -ErrorAction SilentlyContinue

    $entries += [pscustomobject]@{
        name      = $name
        pid       = $processId
        startTime = if ($process) { $process.StartTime.ToString('o') } else { $null }
    }

    $entries | ConvertTo-Json -Depth 3 | Set-Content $PidFile
}

function Stop-TrackedProcesses {
    if (-not (Test-Path $PidFile)) {
        return 0
    }

    $stopped = 0
    $failed = @()

    # Por la variable y no directamente sobre la llamada: la función devuelve la lista
    # envuelta para que no se desenrolle al asignarla, y un `foreach` sobre la llamada
    # recibiría esa lista entera como un único elemento.
    $entries = Read-TrackedEntries

    foreach ($entry in $entries) {
        $process = Get-Process -Id $entry.pid -ErrorAction SilentlyContinue

        if (-not $process) {
            continue
        }

        if (-not (Test-SameProcess $entry $process)) {
            Write-Warn "El PID $($entry.pid) ya no es $($entry.name); no se toca."
            continue
        }

        # taskkill /T y no Stop-Process: lo que se guardó es el PID de la ventana pwsh, y
        # quien escucha en el puerto y bloquea los binarios es su hijo (dotnet o node).
        # Matar solo el padre deja el hijo vivo, y el siguiente `build` falla con el fichero
        # bloqueado mientras el puerto sigue ocupado.
        $output = taskkill /PID $entry.pid /T /F 2>&1

        # Se comprueba que de verdad ha muerto. taskkill falla con "Acceso denegado" cuando el
        # proceso lo arrancó otra sesión, y dar por parado lo que sigue vivo es peor que no
        # pararlo: el arranque siguiente choca con el puerto ocupado sin saber por qué.
        if (Get-Process -Id $entry.pid -ErrorAction SilentlyContinue) {
            Write-Bad "No se ha podido parar $($entry.name) (PID $($entry.pid))."
            Write-Info ($output | Select-Object -First 1)
            $failed += $entry
            continue
        }

        Write-Ok "Parado $($entry.name) y sus procesos hijos (PID $($entry.pid))"
        $stopped++
    }

    # Solo se olvidan los que se han parado: lo que sigue vivo se sigue vigilando, o `down`
    # perdería su rastro y nadie volvería a intentarlo.
    if ($failed.Count -gt 0) {
        $failed | ConvertTo-Json -Depth 3 | Set-Content $PidFile
        Write-Warn 'Quedan procesos vivos. Párralos a mano o repite desde una consola con los mismos permisos.'
    } else {
        Remove-Item $PidFile -ErrorAction SilentlyContinue
    }

    return $stopped
}

function Get-TrackedProcesses {
    # Devuelve siempre un array: sin el `,` de envoltura, PowerShell desenrolla una lista
    # vacía a $null y una de un elemento al elemento suelto, y `.Count` falla con StrictMode.
    if (-not (Test-Path $PidFile)) {
        return , @()
    }

    $entries = Read-TrackedEntries

    $alive = $entries | Where-Object {
        $process = Get-Process -Id $_.pid -ErrorAction SilentlyContinue
        $process -and (Test-SameProcess $_ $process)
    }

    return , @($alive)
}

# ── contenido ────────────────────────────────────────────────────────────────────────────

function Test-ImportUpToDate([string]$source, [string]$slug) {
    <#
        La importación se salta si nada del origen es más nuevo que el manifest ya generado.
        Reimportar 321 lecciones en cada arranque tarda y no cambia nada; con esto el
        segundo `up` es inmediato y el contenido solo se reprocesa cuando de verdad cambió.

        Se compara por fecha de modificación y no por hash: recorrer el contenido entero para
        hashearlo cuesta casi lo mismo que reimportarlo, y aquí basta con detectar ediciones.
    #>
    $manifest = Join-Path $Root "content/manifests/$slug.manifest.json"

    if (-not (Test-Path $manifest)) {
        return $false
    }

    # El destino también tiene que existir: si alguien borró content/dist, hay que copiar
    # de nuevo aunque el manifest siga ahí.
    if (-not (Test-Path (Join-Path $ContentDist $slug))) {
        return $false
    }

    $manifestTime = (Get-Item $manifest).LastWriteTimeUtc

    $newest = if (Test-Path $source -PathType Container) {
        (Get-ChildItem $source -Recurse -File | Measure-Object LastWriteTimeUtc -Maximum).Maximum
    } else {
        (Get-Item $source).LastWriteTimeUtc
    }

    # El propio importador cuenta como origen: si cambia, hay que reimportar aunque el
    # contenido siga igual.
    $tools = (Get-ChildItem (Join-Path $Root 'content/tools') -Filter *.py |
        Measure-Object LastWriteTimeUtc -Maximum).Maximum

    return $manifestTime -gt $newest -and $manifestTime -gt $tools
}

# Cursos que se escriben aquí, en content/cursos, y se generan antes de importar. Un curso solo
# entra en esta lista cuando TODOS sus bloques tienen prosa: publicar la mitad de un temario es
# vender un curso que no está.
$WrittenCourses = @(
    [pscustomobject]@{
        Folder = 'agentes-en-produccion'
        Slug   = 'agentes-en-produccion'
        Title  = 'Agentes en Producción'
    }
)

function Invoke-CourseBuild {
    <#
        Convierte los cursos escritos en Markdown al mismo HTML que consume el importador.
        Los bloques sin prosa se saltan y se dicen: así se ve el avance sin publicar nada a
        medias.
    #>
    Write-Step 'Generando los cursos escritos'

    foreach ($course in $WrittenCourses) {
        $source = Join-Path $Root "content/cursos/$($course.Folder)"

        if (-not (Test-Path $source)) {
            Write-Warn "$($course.Slug): no existe $source"
            continue
        }

        $target = Join-Path $Root "content/generado/$($course.Slug)"

        python (Join-Path $Root 'content/tools/build_course.py') `
            --course $course.Folder `
            --title $course.Title `
            --out $target

        if ($LASTEXITCODE -ne 0) {
            Write-Warn "$($course.Slug): todavía no hay ningún bloque con prosa completa."
        }
    }
}

function Invoke-ContentImport([switch]$Force) {
    Write-Step 'Contenido de los cursos'

    New-Item -ItemType Directory -Force -Path $ContentDist | Out-Null

    if (-not (Test-Path $ContentRoot)) {
        Write-Warn "No se encuentra la formación en: $ContentRoot"
        Write-Info 'La plataforma arranca igual, pero sin cursos que servir.'
        Write-Info 'Define INKOOVA_CONTENT_SOURCE si está en otra ruta.'
        return
    }

    $imported = 0
    $skipped = 0

    foreach ($course in $Courses) {
        $source = Join-Path $ContentRoot $course.Path

        if (-not (Test-Path $source)) {
            Write-Warn "$($course.Slug): no se encuentra $source"
            continue
        }

        if (-not $Force -and (Test-ImportUpToDate $source $course.Slug)) {
            Write-Info "$($course.Slug): sin cambios, se omite"
            $skipped++
            continue
        }

        python (Join-Path $Root 'content/tools/importer.py') `
            --source $source `
            --slug $course.Slug `
            --title $course.Title `
            --out (Join-Path $Root 'content/manifests') `
            --copy-to $ContentDist `
            @($course.Extra)

        if ($LASTEXITCODE -ne 0) {
            throw "La importación de $($course.Slug) ha fallado."
        }

        $imported++
    }

    if ($imported -eq 0 -and $skipped -eq 0) {
        Write-Warn 'No se ha importado ningún curso.'
        return
    }

    if ($imported -eq 0) {
        Write-Ok "Contenido al día ($skipped $(if ($skipped -eq 1) { 'origen' } else { 'orígenes' }) sin cambios)"
        return
    }

    Write-Ok "$imported $(if ($imported -eq 1) { 'origen importado' } else { 'orígenes importados' }) a $ContentDist"
    Write-Info 'Los manifests están en content/manifests. Aplícalos desde /admin/importar.'
    Write-Info 'Para forzar una reimportación: .\dev.ps1 content -Force'
}

# ── base de datos ────────────────────────────────────────────────────────────────────────

function Test-PortListening([int]$port) {
    $client = [System.Net.Sockets.TcpClient]::new()

    try {
        # 300 ms basta en localhost y evita que el bucle de espera se alargue por timeouts.
        if (-not $client.ConnectAsync('127.0.0.1', $port).Wait(300)) {
            return $false
        }

        return $client.Connected
    } catch {
        return $false
    } finally {
        $client.Dispose()
    }
}

function Assert-PortAvailable([int]$port) {
    # Se comprueba antes de llamar a Docker. Si el puerto está ocupado, `docker compose up`
    # deja el contenedor creado pero SIN publicar el puerto, y a partir de ahí arranca "sano"
    # en cada intento mientras la API se conecta a otra base de datos.
    if (-not (Test-PortListening $port)) {
        return
    }

    $owner = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -First 1

    $description = if ($owner) {
        $process = Get-Process -Id $owner.OwningProcess -ErrorAction SilentlyContinue
        if ($process) { "$($process.ProcessName) (PID $($process.Id))" } else { "PID $($owner.OwningProcess)" }
    } else {
        'un proceso desconocido'
    }

    throw "El puerto $port está ocupado por $description. Páralo o cambia PostgresPort en dev.ps1."
}

function Assert-PortPublished {
    <#
        Docker puede tener el contenedor arriba y "healthy" sin haber publicado el puerto, si
        el bind falló al crearlo. El estado del contenedor no lo delata: hay que mirar el mapeo.

        Se usa `{{json .NetworkSettings.Ports}}` y no `{{index ... "5432/tcp"}}`: las comillas
        dobles dentro del argumento sobreviven o no según `$PSNativeCommandArgumentPassing`,
        y donde se pierden docker recibe `5432/tcp` suelto y falla con
        «template parsing error: unexpected "/" in operand». Este formato no las necesita.

        Además no lanza nunca: es una comprobación defensiva y no debe impedir el arranque.
    #>
    $ports = $null

    try {
        $ports = docker inspect inkoova-academy-postgres-1 --format '{{json .NetworkSettings.Ports}}' 2>$null
    } catch {
        return
    }

    if ($LASTEXITCODE -ne 0 -or -not $ports) {
        return
    }

    try {
        $parsed = "$ports" | ConvertFrom-Json
    } catch {
        return
    }

    $mapping = $parsed.PSObject.Properties | Where-Object { $_.Name -eq '5432/tcp' } | Select-Object -First 1

    # Publicado = la clave existe y trae al menos un binding de host.
    if ($mapping -and @($mapping.Value).Count -gt 0) {
        return
    }

    Write-Warn 'El contenedor existe pero no publica el puerto. Recreándolo.'
    docker compose -f $Compose up -d --force-recreate postgres

    if ($LASTEXITCODE -ne 0) {
        throw 'No se ha podido recrear el contenedor de Postgres.'
    }
}

function Start-Database {
    Write-Step "Levantando Postgres en el puerto $PostgresPort"

    # Si el contenedor ya está arriba, lo que escucha en el puerto es nuestro y no hay
    # conflicto que comprobar.
    $running = (docker ps --filter name=inkoova-academy-postgres-1 --format '{{.Names}}' 2>&1) -match 'postgres'

    if (-not $running) {
        Assert-PortAvailable $PostgresPort
    }

    docker compose -f $Compose up -d postgres

    if ($LASTEXITCODE -ne 0) {
        throw 'No se ha podido levantar Postgres. Mira: .\dev.ps1 logs postgres'
    }

    Assert-PortPublished

    Write-Info 'Esperando a que Postgres esté listo...'

    # Hacen falta LAS DOS comprobaciones, y ninguna basta sola:
    #
    #  · El puerto desde el host prueba que Docker publicó el mapeo. `pg_isready` dentro del
    #    contenedor da verde aunque no lo haya publicado, y entonces la API no puede llegar.
    #  · `pg_isready` prueba que Postgres terminó de inicializar el cluster. Docker publica
    #    el puerto en cuanto arranca el contenedor, segundos antes de que Postgres hable su
    #    protocolo: un TCP abierto ahí corta el handshake a media conexión.
    foreach ($attempt in 1..60) {
        if (Test-PortListening $PostgresPort) {
            docker compose -f $Compose exec -T postgres pg_isready -U academy -d academy -q 2>&1 | Out-Null

            if ($LASTEXITCODE -eq 0) {
                Write-Ok "Postgres listo y alcanzable en localhost:$PostgresPort"
                return
            }
        }

        Start-Sleep -Milliseconds 750
    }

    throw "Postgres no está listo en localhost:$PostgresPort tras 45 s. Mira: .\dev.ps1 logs postgres"
}

function Invoke-Migrations {
    Write-Step 'Aplicando migraciones'

    $env:ConnectionStrings__Academy = $ConnectionString

    # La salida se captura para traducir los fallos típicos a algo accionable: el stack de
    # Npgsql tiene cuarenta líneas y la útil es la primera.
    $output = dotnet run --project (Join-Path $Root 'api/src/Inkoova.Academy.Migrator') --verbosity quiet 2>&1
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        $text = $output -join "`n"

        if ($text -match '28P01|password authentication failed') {
            throw "Postgres ha rechazado las credenciales en el puerto $PostgresPort. " +
                  'Casi siempre significa que ahi responde OTRO Postgres, no el del contenedor. ' +
                  'Empieza de cero con: .\dev.ps1 reset'
        }

        if ($text -match 'refused|No connection could be made|EndOfStream') {
            throw "Postgres no acepta conexiones en localhost:$PostgresPort. Prueba: .\dev.ps1 restart"
        }

        Write-Host $text
        throw 'Las migraciones han fallado.'
    }

    Write-Ok 'Esquema al día'
}

# ── entorno de la API ────────────────────────────────────────────────────────────────────

function Get-ApiEnvironment {
    # Secretos de desarrollo, visiblemente falsos. No se comparten con producción: allí vienen
    # de infra/.env y los genera bootstrap.sh.
    #
    # Las claves de Stripe son placeholders: sin ellas la API no arranca (falla al validar la
    # configuración, que es lo que queremos), pero con ellas tampoco se puede cobrar. Para
    # probar pagos, exporta STRIPE_SECRET_KEY y STRIPE_WEBHOOK_SECRET antes de llamar.
    @{
        'ASPNETCORE_ENVIRONMENT'               = 'Development'
        'ASPNETCORE_URLS'                      = $ApiUrl
        'ConnectionStrings__Academy'           = $ConnectionString
        'Academy__PublicBaseUrl'               = $WebUrl
        'Academy__ContentRoot'                 = $ContentDist
        'Academy__ContentTokenKey'             = 'dev-content-token-key-change-me'
        'Academy__Jwt__SigningKey'             = 'dev-jwt-signing-key-at-least-32-bytes-long!'
        'Academy__Certificates__SigningSecret' = 'dev-certificate-secret'
        'Academy__Email__TemplatesPath'        = (Join-Path $Root 'emails')
        'Academy__Cors__Origins__0'            = $WebUrl
        'Academy__Auth__RequireConfirmedEmail' = 'false'
        # Administradores globales. Reciben el rol al registrarse y en cada arranque, así que
        # sobreviven a `.\dev.ps1 reset`. En producción se definen en infra/.env.
        'Academy__Auth__OwnerEmails'           = $OwnerEmails -join ','
        'Academy__Stripe__SecretKey'           = if ($env:STRIPE_SECRET_KEY) { $env:STRIPE_SECRET_KEY } else { 'sk_test_placeholder' }
        'Academy__Stripe__WebhookSecret'       = if ($env:STRIPE_WEBHOOK_SECRET) { $env:STRIPE_WEBHOOK_SECRET } else { 'whsec_placeholder' }
        # Admin de arranque, solo en local. En producción se define en infra/.env.
        'Academy__Seed__AdminEmail'            = 'admin@localhost'
        'Academy__Seed__AdminPassword'         = 'DesarrolloLocal2026!'
        'Academy__Seed__AdminName'             = 'Administración local'
    }
}

function Start-Api {
    Write-Step 'Arrancando la API'

    # La configuración va por VARIABLES DE ENTORNO, no por línea de comandos: el doble guion
    # bajo solo se interpreta como jerarquía (Academy__Jwt__SigningKey → Academy:Jwt:SigningKey)
    # en variables de entorno. Como argumento quedaría una clave literal que la API no
    # encuentra, y arrancaría quejándose de que falta el secreto.
    $variables = Get-ApiEnvironment

    $assignments = ($variables.GetEnumerator() | ForEach-Object {
        '$env:' + $_.Key + " = '" + ($_.Value -replace "'", "''") + "'"
    }) -join "`n"

    # --no-launch-profile: sin él, `dotnet run` aplica launchSettings.json y su applicationUrl
    # pisa a ASPNETCORE_URLS, dejando la API escuchando en otro puerto.
    $script = $assignments + "`n" +
        "Set-Location '$Root'`n" +
        'dotnet run --project api/src/Inkoova.Academy.Api --no-launch-profile'

    $process = Start-Process pwsh -ArgumentList '-NoExit', '-NoProfile', '-Command', $script -PassThru

    Save-Process 'api' $process.Id
    Write-Ok "API arrancando en $ApiUrl (PID $($process.Id))"
    Write-Info "Documentación de la API: $ApiUrl/scalar/v1"
}

function Start-Web {
    Write-Step 'Arrancando la web'

    $webRoot = Join-Path $Root 'web'

    if (-not (Test-Path (Join-Path $webRoot 'node_modules'))) {
        Write-Info 'Instalando dependencias de npm (solo la primera vez)...'
        Push-Location $webRoot
        npm.cmd install --no-audit --no-fund
        $installExitCode = $LASTEXITCODE
        Pop-Location

        if ($installExitCode -ne 0) {
            throw 'No se han podido instalar las dependencias de la web.'
        }
    }

    $nodePath = (Get-Command node -ErrorAction Stop).Source
    $vitePath = Join-Path $webRoot 'node_modules/vite/bin/vite.js'

    if (-not (Test-Path $vitePath)) {
        throw 'No se encuentra Vite. Ejecuta npm.cmd --prefix web ci.'
    }

    $childScript = "Set-Location '$webRoot'; & '$nodePath' '$vitePath'"

    $process = Start-Process pwsh `
        -ArgumentList '-NoExit', '-NoProfile', '-Command', $childScript `
        -PassThru

    Save-Process 'web' $process.Id
    Write-Ok "Web arrancando en $WebUrl (PID $($process.Id))"
}

function Wait-ApiReady {
    Write-Info 'Esperando a que la API responda...'

    foreach ($attempt in 1..90) {
        try {
            $response = Invoke-WebRequest -Uri "$ApiUrl/health/ready" -TimeoutSec 2 -ErrorAction Stop

            if ($response.StatusCode -eq 200) {
                Write-Ok 'API lista'
                return $true
            }
        } catch {
            # Todavía arrancando: se reintenta.
        }

        Start-Sleep -Seconds 1
    }

    Write-Warn 'La API no ha respondido en 90 segundos.'
    Write-Info 'El error está en la ventana que se ha abierto para la API.'
    return $false
}

# ── comandos ─────────────────────────────────────────────────────────────────────────────

function Invoke-Up {
    Assert-Prerequisites -RequireDocker -RequireNode:(-not $NoWeb) -RequirePython:(-not $SkipContent)

    if ($Full) {
        Write-Step 'Levantando todo en Docker Compose'

        if (-not $SkipContent) { Invoke-ContentImport }

        docker compose -f $Compose up -d --build

        if ($LASTEXITCODE -ne 0) {
            throw 'docker compose up ha fallado.'
        }

        Write-Ok 'Compose levantado'
        Write-Info "API: $ApiUrl"
        Write-Info 'La web no está en este compose: usa .\dev.ps1 up sin -Full, o npm --prefix web run dev'
        return
    }

    if ((Get-TrackedProcesses).Count -gt 0) {
        Write-Warn 'Ya hay procesos levantados por este script. Usa .\dev.ps1 restart'
        Invoke-Status
        return
    }

    if (-not $SkipContent) { Invoke-ContentImport }

    Start-Database
    Invoke-Migrations
    Start-Api

    # Se espera a la API ANTES de levantar la web. Vite arranca en un cuarto de segundo y la API
    # tarda casi un minuto en estar lista: entre medias, cualquier pestaña abierta reintenta
    # contra un puerto muerto y la consola se llena de "http proxy error: ECONNREFUSED" que
    # parecen un fallo del código y no lo son. Esperando aquí, cuando la web abre hay alguien
    # al otro lado.
    $apiReady = Wait-ApiReady

    if (-not $NoWeb) { Start-Web }

    if ($apiReady) {
        Write-Host ''
        Write-Host '  Todo listo.' -ForegroundColor Green
        Write-Host ''
        Write-Info "Web         $WebUrl"
        Write-Info "API         $ApiUrl"
        Write-Info "API docs    $ApiUrl/scalar/v1"
        Write-Info 'Admin       admin@localhost / DesarrolloLocal2026!'
        Write-Host ''
        Write-Info 'Siguiente paso: entra en /admin/importar y aplica los manifests de content/manifests.'
        Write-Info 'Para parar todo: .\dev.ps1 down'
    }
}

function Invoke-Down {
    Write-Step 'Parando'

    $stopped = Stop-TrackedProcesses

    if ($stopped -eq 0) {
        Write-Info 'No había procesos nativos levantados por este script.'
    }

    if ((Get-Command docker -ErrorAction SilentlyContinue) -and (Test-DockerRunning)) {
        docker compose -f $Compose stop 2>&1 | Out-Null
        Write-Ok 'Contenedores parados (los datos se conservan)'
    }
}

function Invoke-Reset {
    Write-Host ''
    Write-Host '  Esto BORRA la base de datos local por completo.' -ForegroundColor Yellow
    Write-Host '  Se pierden usuarios, cursos importados, progreso y certificados de desarrollo.' -ForegroundColor Yellow
    Write-Host ''

    $answer = Read-Host '  Escribe BORRAR para confirmar'

    if ($answer -cne 'BORRAR') {
        Write-Info 'Cancelado. No se ha borrado nada.'
        return
    }

    Stop-TrackedProcesses | Out-Null

    Write-Step 'Borrando volúmenes'
    docker compose -f $Compose down -v
    Write-Ok 'Base de datos eliminada. Ejecuta .\dev.ps1 up para empezar de cero.'
}

function Invoke-Verify {
    Assert-Prerequisites -RequireNode -RequirePython -RequireDocker:$Integration

    $failures = @()

    Write-Step 'Compilando la solución .NET'
    dotnet build (Join-Path $Root 'api/Inkoova.Academy.slnx') --nologo --verbosity quiet
    if ($LASTEXITCODE -ne 0) { $failures += 'build .NET' } else { Write-Ok 'Compila sin avisos' }

    Write-Step 'Tests de dominio'
    dotnet test (Join-Path $Root 'api/tests/Inkoova.Academy.Domain.Tests') --nologo --verbosity quiet --no-build
    if ($LASTEXITCODE -ne 0) { $failures += 'tests de dominio' } else { Write-Ok 'Tests de dominio en verde' }

    if ($Integration) {
        Write-Step 'Tests de integración (Testcontainers)'
        dotnet test (Join-Path $Root 'api/tests/Inkoova.Academy.Integration.Tests') --nologo --verbosity quiet --no-build
        if ($LASTEXITCODE -ne 0) { $failures += 'tests de integración' } else { Write-Ok 'Tests de integración en verde' }
    } else {
        Write-Warn 'Tests de integración omitidos. Añade -Integration (necesitan Docker).'
    }

    Write-Step 'Tests del importador'
    python (Join-Path $Root 'content/tools/test_importer.py') 2>&1 | Select-Object -Last 3
    if ($LASTEXITCODE -ne 0) { $failures += 'tests del importador' } else { Write-Ok 'Importador en verde' }

    Write-Step 'Agente Meridiana sobre el conjunto sintético'
    Push-Location (Join-Path $Root 'content/caso/meridiana-agent')
    $verdict = (dotnet run --verbosity quiet -- --all --check 2>&1 | Select-String 'coinciden' | Select-Object -Last 1)
    if ($LASTEXITCODE -ne 0) {
        $failures += 'agente Meridiana'
        Write-Bad "$verdict"
    } else {
        Write-Ok "$verdict".Trim()
    }
    Pop-Location

    Push-Location (Join-Path $Root 'web')

    if (-not (Test-Path 'node_modules')) {
        Write-Info 'Instalando dependencias de npm...'
        npm install --no-audit --no-fund
    }

    Write-Step 'Comprobación de tipos de la web'
    npx tsc --noEmit -p tsconfig.json
    if ($LASTEXITCODE -ne 0) { $failures += 'typecheck de la web' } else { Write-Ok 'Tipos correctos' }

    Write-Step 'Lint de la web'
    npx eslint . --max-warnings 0
    if ($LASTEXITCODE -ne 0) { $failures += 'lint de la web' } else { Write-Ok 'Lint limpio' }

    Write-Step 'Build de la web'
    npm run build 2>&1 | Select-String 'built in|error' | Select-Object -Last 1
    if ($LASTEXITCODE -ne 0) { $failures += 'build de la web' } else { Write-Ok 'Build correcto' }

    Pop-Location

    Write-Host ''

    if ($failures.Count -eq 0) {
        Write-Host '  Todas las comprobaciones en verde.' -ForegroundColor Green
        # Sin `return 0`: en PowerShell ese 0 se escribiría en la salida estándar.
        return
    }

    Write-Host "  $($failures.Count) comprobaciones han fallado:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Bad $_ }
    exit 1
}

function Invoke-Test {
    Assert-Prerequisites -RequirePython -RequireDocker:$Integration

    dotnet test (Join-Path $Root 'api/tests/Inkoova.Academy.Domain.Tests') --nologo
    python (Join-Path $Root 'content/tools/test_importer.py')

    if ($Integration) {
        dotnet test (Join-Path $Root 'api/tests/Inkoova.Academy.Integration.Tests') --nologo
    } else {
        Write-Warn 'Tests de integración omitidos. Añade -Integration.'
    }
}

function Invoke-Logs {
    $service = if ($Rest -and $Rest.Count -gt 0) { $Rest[0] } else { $null }

    if ($service) {
        docker compose -f $Compose logs -f --tail 100 $service
    } else {
        docker compose -f $Compose logs -f --tail 100
    }
}

function Invoke-Status {
    Write-Step 'Procesos nativos'

    $tracked = Get-TrackedProcesses

    if ($tracked.Count -eq 0) {
        Write-Info 'Ninguno levantado por este script.'
    } else {
        $tracked | ForEach-Object { Write-Ok "$($_.name) · PID $($_.pid)" }
    }

    Write-Step 'Contenedores'

    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Write-Info 'Docker no está instalado.'
    } elseif (-not (Test-DockerRunning)) {
        Write-Info 'El demonio de Docker no responde.'
    } else {
        docker compose -f $Compose ps
    }

    Write-Step 'Endpoints'

    foreach ($endpoint in @(
        @{ Name = 'API'; Url = "$ApiUrl/health/ready" },
        @{ Name = 'Web'; Url = $WebUrl }
    )) {
        try {
            $response = Invoke-WebRequest -Uri $endpoint.Url -TimeoutSec 2 -ErrorAction Stop
            Write-Ok "$($endpoint.Name) responde ($($response.StatusCode))"
        } catch {
            Write-Info "$($endpoint.Name) no responde"
        }
    }
}

function Invoke-Catalog([string]$name) {
    # Delegados en sus propios scripts: el traslado se usa también desde el servidor, donde
    # `dev.ps1` no pinta nada porque allí no se levanta ningún entorno de desarrollo.
    $script = Join-Path $Root "infra/$name.ps1"

    if (-not (Test-Path $script)) {
        throw "No se encuentra $script."
    }

    # Los argumentos sobrantes se reconstruyen en una tabla en vez de reenviarse con `@Rest`.
    # El splat de un ARRAY enlaza por posición, no por nombre: `@('-Package','X')` llegaría al
    # otro lado como dos argumentos posicionales y fallaría con «a positional parameter cannot
    # be found». Solo el splat de una tabla hash enlaza por nombre.
    $named = @{}
    $positional = @()

    # `$Rest` es $null cuando no se pasó ningún argumento suelto, y con StrictMode pedirle
    # .Count a $null es un error. Envolverlo lo deja siempre como una lista.
    $tokens = @($Rest)

    for ($i = 0; $i -lt $tokens.Count; $i++) {
        $token = $tokens[$i]

        if ($token -notlike '-*') {
            $positional += $token
            continue
        }

        $key = $token.TrimStart('-')
        $next = if ($i + 1 -lt $tokens.Count) { $tokens[$i + 1] } else { $null }

        if ($null -ne $next -and $next -notlike '-*') {
            # Un valor que empiece por guion se confundiría con el siguiente parámetro. Es la
            # única forma que no se puede reenviar; para eso está llamar al script directamente.
            $named[$key] = $next
            $i++
        } else {
            $named[$key] = $true
        }
    }

    & $script @named @positional
}

function Invoke-Help {
    Write-Host @'

  Inkoova Academy · desarrollo local

  USO
    .\dev.ps1 <comando> [opciones]

  COMANDOS
    up         Levanta todo: contenido, Postgres, migraciones, API y web.
    down       Para lo que este script haya levantado. Conserva los datos.
    restart    down y up.
    reset      Para todo y BORRA la base de datos. Pide confirmación.
    content    Importa el contenido que haya cambiado.  -Force lo reimporta todo.
    export     Empaqueta el catálogo y la configuración —sin usuarios— en export/catalog-<fecha>.
    import     Aplica un paquete en una base.  .\dev.ps1 import -Package <carpeta> -DryRun
    verify     Compila y ejecuta todas las comprobaciones.
    test       Solo los tests.
    logs       Sigue los logs de los contenedores.  .\dev.ps1 logs postgres
    status     Qué está levantado ahora mismo.

  OPCIONES
    -Full          Todo en Docker Compose en vez de API y web nativas.
    -NoWeb         No arranca la web.
    -SkipContent   No reimporta el contenido al arrancar (el primer arranque es lo lento).
    -Integration   Incluye los tests de integración (necesitan Docker).
    -Force         Reimporta el contenido aunque no haya cambiado.

  PRIMERA VEZ
    .\dev.ps1 up
    Después, entra en http://localhost:5173, inicia sesión como admin@localhost
    y aplica los manifests desde /admin/importar.

    WEB AISLADA
        npm.cmd --prefix web run dev

  VARIABLES DE ENTORNO OPCIONALES
    INKOOVA_CONTENT_SOURCE   Raíz de la formación si no está junto al repositorio.
    STRIPE_SECRET_KEY        Para probar pagos de verdad en modo test.
    STRIPE_WEBHOOK_SECRET    Para verificar la firma del webhook.

'@ -ForegroundColor Gray
}

# ── despacho ─────────────────────────────────────────────────────────────────────────────

try {
    switch ($Command) {
        'up'      { Invoke-Up }
        'down'    { Invoke-Down }
        'restart' { Invoke-Down; Start-Sleep -Seconds 2; Invoke-Up }
        'reset'   { Invoke-Reset }
        'content' { Assert-Prerequisites -RequirePython; Invoke-ContentImport -Force:$Force }
        'export'  { Invoke-Catalog 'export-catalog' }
        'import'  { Invoke-Catalog 'import-catalog' }
        'verify'  { Invoke-Verify }
        'test'    { Invoke-Test }
        'logs'    { Invoke-Logs }
        'status'  { Invoke-Status }
        default   { Invoke-Help }
    }

    # Explícito: sin esto el script hereda el código de salida del último comando externo, y
    # un `docker info` que falla a propósito haría que `status` pareciera un error.
    exit 0
} catch {
    Write-Host ''
    Write-Bad $_.Exception.Message
    Write-Host ''
    exit 1
}
