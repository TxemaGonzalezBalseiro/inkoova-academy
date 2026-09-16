using System.Data;
using Dapper;

namespace Inkoova.Academy.Infrastructure.Persistence;

/// <summary>
/// Dapper needs to be told how to move types Postgres does not model natively.
/// Registered once at startup; registering twice is harmless but wasteful.
/// </summary>
public static class DapperTypeHandlers
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        SqlMapper.AddTypeHandler(new DateOnlyHandler());
        SqlMapper.AddTypeHandler(new NullableDateOnlyHandler());

        // El anulable va DESPUÉS: registrar el handler de un tipo por valor registra de paso
        // uno derivado para su `Nullable<>`, y este lo tiene que pisar.
        SqlMapper.AddTypeHandler(new UtcDateTimeOffsetHandler());
        SqlMapper.AddTypeHandler(new NullableUtcDateTimeOffsetHandler());

        // Cubre el mapeo por propiedades…
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        // …y esto el de constructores, que MatchNamesWithUnderscores no toca. Los tipos de
        // fila son records posicionales: sin este registro, Dapper no encuentra constructor.
        SnakeCaseTypeMapRegistration.RegisterAll();

        _registered = true;
    }

    private sealed class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override DateOnly Parse(object value) => value switch
        {
            DateOnly d => d,
            DateTime dt => DateOnly.FromDateTime(dt),
            string s => DateOnly.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new InvalidCastException($"No se puede convertir {value.GetType()} a DateOnly.")
        };

        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.ToDateTime(TimeOnly.MinValue);
        }
    }

    /// <summary>
    /// Pasa a UTC todo <see cref="DateTimeOffset"/> que vaya a la base.
    ///
    /// Las columnas de instante son <c>timestamptz</c> y Npgsql rechaza escribir un
    /// desplazamiento distinto de cero: «Cannot write DateTimeOffset with Offset=01:00:00 to
    /// PostgreSQL type 'timestamp with time zone', only offset 0 (UTC) is supported».
    ///
    /// La API ya normaliza lo que entra por JSON, pero la restricción vive aquí, así que la
    /// conversión también: cubre además lo que nunca pasa por el serializador —los jobs, los
    /// objetos del SDK de Stripe, los seeds y cualquier fecha compuesta en código—.
    /// </summary>
    private sealed class UtcDateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override DateTimeOffset Parse(object value) =>
            ParseUtc(value) ?? throw new InvalidCastException(
                $"No se puede convertir {value.GetType()} a DateTimeOffset.");

        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        {
            parameter.DbType = DbType.DateTimeOffset;
            parameter.Value = value.ToUniversalTime();
        }
    }

    private sealed class NullableUtcDateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset?>
    {
        public override DateTimeOffset? Parse(object? value) => value switch
        {
            null or DBNull => null,
            _ => ParseUtc(value) ?? throw new InvalidCastException(
                $"No se puede convertir {value.GetType()} a DateTimeOffset?.")
        };

        public override void SetValue(IDbDataParameter parameter, DateTimeOffset? value)
        {
            parameter.DbType = DbType.DateTimeOffset;
            parameter.Value = value.HasValue ? value.Value.ToUniversalTime() : DBNull.Value;
        }
    }

    /// <summary>
    /// Npgsql devuelve un <c>timestamptz</c> como <see cref="DateTime"/> con
    /// <c>Kind=Utc</c>, no como <see cref="DateTimeOffset"/>, así que hay que contemplar los
    /// dos. <c>null</c> significa «este tipo no se sabe convertir»; quien llama decide el error.
    /// </summary>
    private static DateTimeOffset? ParseUtc(object value) => value switch
    {
        DateTimeOffset d => d.ToUniversalTime(),
        DateTime { Kind: DateTimeKind.Local } local => new DateTimeOffset(local).ToUniversalTime(),
        DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
        string s => DateTimeOffset.Parse(s, System.Globalization.CultureInfo.InvariantCulture)
            .ToUniversalTime(),
        _ => null
    };

    private sealed class NullableDateOnlyHandler : SqlMapper.TypeHandler<DateOnly?>
    {
        public override DateOnly? Parse(object? value) => value switch
        {
            null or DBNull => null,
            DateOnly d => d,
            DateTime dt => DateOnly.FromDateTime(dt),
            string s => DateOnly.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new InvalidCastException($"No se puede convertir {value.GetType()} a DateOnly?.")
        };

        public override void SetValue(IDbDataParameter parameter, DateOnly? value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.HasValue ? value.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
        }
    }
}

/// <summary>
/// Enum values are stored lowercase (the CHECK constraints in the migrations spell them
/// out), so the mapping lives in one place instead of at every call site.
/// </summary>
public static class EnumMapping
{
    public static string ToDb<TEnum>(TEnum value) where TEnum : struct, Enum =>
        value.ToString()!.ToLowerInvariant();

    public static TEnum FromDb<TEnum>(string value) where TEnum : struct, Enum =>
        Enum.Parse<TEnum>(value, ignoreCase: true);

    public static TEnum? FromDbNullable<TEnum>(string? value) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? null : Enum.Parse<TEnum>(value, ignoreCase: true);
}
