using System.Reflection;
using DbUp;

using Microsoft.Extensions.Configuration;

// Applies db/migrations against the configured database and exits.
// Runs as a one-shot container before the API starts (infra/docker-compose.yml),
// and as a step in CI to prove the schema builds from zero (T-01 acceptance criteria).

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var connectionString = configuration.GetConnectionString("Academy")
                       ?? configuration["ConnectionStrings__Academy"]
                       ?? Environment.GetEnvironmentVariable("ACADEMY_DB");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "Falta la cadena de conexión. Usa ConnectionStrings__Academy o la variable ACADEMY_DB.");
    return 2;
}

var scriptsPath = configuration["MigrationsPath"] ?? FindMigrationsDirectory();

if (scriptsPath is null || !Directory.Exists(scriptsPath))
{
    Console.Error.WriteLine($"No se encuentra el directorio de migraciones: {scriptsPath ?? "(no resuelto)"}");
    return 2;
}

Console.WriteLine($"Aplicando migraciones desde {scriptsPath}");

// DbUp waits for Postgres to accept connections: in compose the API container starts
// before the database has finished its first-run initialisation.
EnsureDatabase.For.PostgresqlDatabase(connectionString);

var upgrader = DeployChanges.To
    .PostgresqlDatabase(connectionString)
    .WithScriptsFromFileSystem(scriptsPath)
    .WithTransactionPerScript()
    .LogToConsole()
    .Build();

if (!upgrader.IsUpgradeRequired())
{
    Console.WriteLine("Sin migraciones pendientes.");
    return 0;
}

var result = upgrader.PerformUpgrade();

if (!result.Successful)
{
    Console.Error.WriteLine($"Migración fallida: {result.Error}");
    return 1;
}

Console.WriteLine($"Aplicadas {result.Scripts.Count()} migraciones.");
return 0;

// Walks up from the binary looking for db/migrations, so the tool works from the
// repo root, from the project directory and from the published container image.
static string? FindMigrationsDirectory()
{
    var candidates = new List<string>
    {
        Path.Combine(AppContext.BaseDirectory, "migrations"),
        "/app/migrations"
    };

    var directory = new DirectoryInfo(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory);

    while (directory is not null)
    {
        candidates.Add(Path.Combine(directory.FullName, "db", "migrations"));
        directory = directory.Parent;
    }

    return candidates.FirstOrDefault(Directory.Exists);
}
