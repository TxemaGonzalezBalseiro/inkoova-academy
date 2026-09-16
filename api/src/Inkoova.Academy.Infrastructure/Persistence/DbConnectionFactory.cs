using System.Data;
using Npgsql;

namespace Inkoova.Academy.Infrastructure.Persistence;

public interface IDbConnectionFactory
{
    Task<IDbConnection> OpenAsync(CancellationToken ct);
}

/// <summary>
/// Opens connections from the Npgsql pool. Built once per process: the data source owns the
/// pool, so creating it per request would defeat pooling entirely.
/// </summary>
public sealed class NpgsqlConnectionFactory : IDbConnectionFactory, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlConnectionFactory(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        _dataSource = builder.Build();
    }

    public async Task<IDbConnection> OpenAsync(CancellationToken ct) =>
        await _dataSource.OpenConnectionAsync(ct);

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
