using System.Reflection;
using Dapper;

namespace Inkoova.Academy.Infrastructure.Persistence;

/// <summary>
/// Mapea columnas <c>snake_case</c> a parámetros de constructor en PascalCase.
///
/// Existe porque <c>DefaultTypeMap.MatchNamesWithUnderscores</c> **solo se aplica al mapeo
/// por propiedades**, no al de constructores. Los tipos de fila de este proyecto son records
/// posicionales, así que Dapper busca un constructor cuyos parámetros se llamen exactamente
/// como las columnas (<c>product_id</c>) y no encuentra ninguno:
///
///     A parameterless default constructor or one matching signature
///     (System.Guid id, System.Guid product_id, ...) is required for ... materialization
///
/// Las alternativas eran poner un alias en cada columna de cada SELECT, o convertir todos los
/// records de fila a propiedades mutables. Esto resuelve el problema en un sitio y deja el
/// SQL tal como se escribió.
/// </summary>
public sealed class SnakeCaseTypeMap<T> : SqlMapper.ITypeMap
{
    private readonly SqlMapper.ITypeMap _fallback = new DefaultTypeMap(typeof(T));

    public ConstructorInfo? FindConstructor(string[] names, Type[] types)
    {
        // Se elige el constructor cuyos parámetros cubran todas las columnas devueltas,
        // comparando sin guiones bajos ni mayúsculas.
        foreach (var constructor in typeof(T).GetConstructors())
        {
            var parameters = constructor.GetParameters();

            if (parameters.Length != names.Length)
            {
                continue;
            }

            var matches = names.All(name =>
                parameters.Any(parameter => IsMatch(parameter.Name, name)));

            if (matches)
            {
                return constructor;
            }
        }

        return _fallback.FindConstructor(names, types);
    }

    public SqlMapper.IMemberMap? GetConstructorParameter(ConstructorInfo constructor, string columnName)
    {
        var parameter = constructor.GetParameters()
            .FirstOrDefault(candidate => IsMatch(candidate.Name, columnName));

        return parameter is null ? null : new ParameterMemberMap(columnName, parameter);
    }

    public ConstructorInfo? FindExplicitConstructor() => _fallback.FindExplicitConstructor();

    public SqlMapper.IMemberMap? GetMember(string columnName) => _fallback.GetMember(columnName);

    /// <summary>
    /// <c>product_id</c> equivale a <c>ProductId</c>: se comparan solo letras y dígitos,
    /// ignorando mayúsculas.
    /// </summary>
    private static bool IsMatch(string? parameterName, string columnName) =>
        parameterName is not null
        && string.Equals(Normalize(parameterName), Normalize(columnName), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit));

    private sealed class ParameterMemberMap(string columnName, ParameterInfo parameter) : SqlMapper.IMemberMap
    {
        public string ColumnName => columnName;

        public Type MemberType => parameter.ParameterType;

        public PropertyInfo? Property => null;

        public FieldInfo? Field => null;

        public ParameterInfo Parameter => parameter;
    }
}

public static class SnakeCaseTypeMapRegistration
{
    /// <summary>
    /// Registra el mapa para cada tipo de fila del ensamblado de infraestructura y para los
    /// records de <c>Application.Abstractions</c> que se consultan directamente.
    ///
    /// Se descubren por reflexión en vez de listarlos a mano: una fila nueva que alguien
    /// olvide registrar fallaría en tiempo de ejecución y solo al devolver datos, que es la
    /// forma más incómoda de enterarse.
    /// </summary>
    public static void RegisterAll()
    {
        var infrastructure = typeof(SnakeCaseTypeMapRegistration).Assembly.GetTypes();

        var candidates = infrastructure
            // Los tipos de fila anidados en los repositorios (CourseRow, PlanRow, …).
            .SelectMany(type => type.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            // Y los de primer nivel: IdentityUserRow y RefreshTokenRow viven fuera de una clase.
            .Concat(infrastructure)
            // Los records de Application que se consultan directamente (FiscalInvoice, …).
            .Concat(typeof(Application.Abstractions.IClock).Assembly.GetTypes())
            .Where(IsPositionalRecord)
            .Distinct();

        foreach (var type in candidates)
        {
            SqlMapper.SetTypeMap(
                type,
                (SqlMapper.ITypeMap)Activator.CreateInstance(typeof(SnakeCaseTypeMap<>).MakeGenericType(type))!);
        }
    }

    /// <summary>
    /// Un record posicional: tiene el método sintetizado <c>&lt;Clone&gt;$</c> y un único
    /// constructor con parámetros. Los demás tipos se dejan al mapeo por defecto.
    /// </summary>
    private static bool IsPositionalRecord(Type type)
    {
        if (!type.IsClass && !type.IsValueType)
        {
            return false;
        }

        if (type.IsGenericTypeDefinition || type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is null)
        {
            return false;
        }

        var constructors = type.GetConstructors();
        return constructors.Length > 0 && constructors.Any(c => c.GetParameters().Length > 0);
    }
}
