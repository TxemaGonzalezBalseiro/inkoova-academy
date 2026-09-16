using DbUp;
using Testcontainers.PostgreSql;

namespace Inkoova.Academy.Integration.Tests;

/// <summary>
/// One Postgres 17 container per test run, migrated from zero with the real DbUp scripts.
/// That is the point: it proves the migrations apply from an empty database on every CI run
/// (T-01 acceptance criteria), not just that the code compiles against a schema someone
/// created by hand.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("academy")
        .WithUsername("academy")
        .WithPassword("academy")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ApplyMigrations(ConnectionString);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public static void ApplyMigrations(string connectionString)
    {
        var scripts = FindMigrationsDirectory()
                      ?? throw new InvalidOperationException("No se encuentra db/migrations desde el directorio de tests.");

        var result = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsFromFileSystem(scripts)
            .WithTransactionPerScript()
            .LogToNowhere()
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
        {
            throw new InvalidOperationException($"Migración fallida: {result.Error}");
        }
    }

    private static string? FindMigrationsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "db", "migrations");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "postgres";
}
