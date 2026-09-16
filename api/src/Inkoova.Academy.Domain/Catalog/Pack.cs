using Inkoova.Academy.Domain.Common;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Catalog;

/// <summary>One downloadable file of a sector pack. Immutable inside a pack version.</summary>
public sealed record PackFile(
    Guid Id,
    Guid PackId,
    string FileName,
    string ContentRef,
    long SizeInBytes,
    string Sha256,
    int Order);

/// <summary>
/// Sector pack (C-15..C-20): a versioned set of documents sold one-off or included in the
/// yearly and lifetime plans. The version is semantic and drives the "new version" email.
/// </summary>
public sealed class Pack
{
    private readonly List<PackFile> _files = [];

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Slug Slug { get; private set; }
    public string Title { get; private set; }
    public string Sector { get; private set; }
    public string Version { get; private set; }
    public string? Changelog { get; private set; }
    public PublicationStatus Status { get; private set; }

    /// <summary>Date the regulatory content was last verified. Printed on every file cover (C-15).</summary>
    public DateOnly? RegulatoryCheckDate { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<PackFile> Files => _files;

    private Pack(
        Guid id,
        Guid productId,
        Slug slug,
        string title,
        string sector,
        string version,
        string? changelog,
        PublicationStatus status,
        DateOnly? regulatoryCheckDate,
        DateTimeOffset updatedAt)
    {
        Id = id;
        ProductId = productId;
        Slug = slug;
        Title = title;
        Sector = sector;
        Version = version;
        Changelog = changelog;
        Status = status;
        RegulatoryCheckDate = regulatoryCheckDate;
        UpdatedAt = updatedAt;
    }

    public static Result<Pack, Error> Create(
        Guid id,
        Guid productId,
        Slug slug,
        string title,
        string sector,
        string version,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("pack.title_empty", "El pack necesita un título.");
        }

        if (!IsSemanticVersion(version))
        {
            return Error.Validation("pack.version_invalid", "La versión del pack debe ser semántica, p. ej. 1.0.0.");
        }

        return new Pack(
            id, productId, slug, title.Trim(), sector.Trim(), version,
            changelog: null, PublicationStatus.Draft, regulatoryCheckDate: null, now);
    }

    public static Pack Rehydrate(
        Guid id,
        Guid productId,
        Slug slug,
        string title,
        string sector,
        string version,
        string? changelog,
        PublicationStatus status,
        DateOnly? regulatoryCheckDate,
        DateTimeOffset updatedAt,
        IEnumerable<PackFile> files)
    {
        var pack = new Pack(id, productId, slug, title, sector, version, changelog, status, regulatoryCheckDate, updatedAt);
        pack._files.AddRange(files.OrderBy(f => f.Order));
        return pack;
    }

    public Result<Unit, Error> AddFile(PackFile file)
    {
        if (file.PackId != Id)
        {
            return Error.Validation("pack.file_mismatch", "El fichero pertenece a otro pack.");
        }

        if (_files.Any(f => f.FileName.Equals(file.FileName, StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict("pack.file_duplicated", $"El pack ya contiene un fichero llamado '{file.FileName}'.");
        }

        _files.Add(file);
        return Unit.Value;
    }

    /// <summary>
    /// Publishing a new version is what triggers the notification to previous buyers (T-11),
    /// so it refuses to move backwards or to republish the same version.
    ///
    /// La PRIMERA publicación es la excepción, y hace falta: un pack nace con su versión ya
    /// escrita —la que le puso el catálogo al crearlo— y sin haberse publicado nunca. Exigirle
    /// una versión mayor obligaría a saltarse la 1.0.0 y estrenar en la 1.0.1, que es mentir
    /// sobre el historial para satisfacer una comprobación pensada para las actualizaciones.
    ///
    /// Nadie ha descargado nada todavía, así que tampoco hay a quién avisar de un cambio.
    /// </summary>
    public Result<Unit, Error> ReleaseVersion(string newVersion, string changelog, DateOnly regulatoryCheckDate, DateTimeOffset now)
    {
        if (!IsSemanticVersion(newVersion))
        {
            return Error.Validation("pack.version_invalid", "La versión del pack debe ser semántica, p. ej. 1.1.0.");
        }

        var esPrimera = Status != PublicationStatus.Published;

        if (!esPrimera && CompareVersions(newVersion, Version) <= 0)
        {
            return Error.Conflict(
                "pack.version_not_greater",
                $"La versión {newVersion} no es posterior a la actual {Version}.");
        }

        // En la primera sí se impide retroceder: publicar por debajo de la versión con la que el
        // pack fue creado dejaría el catálogo diciendo una cosa y el pack otra.
        if (esPrimera && CompareVersions(newVersion, Version) < 0)
        {
            return Error.Conflict(
                "pack.version_not_greater",
                $"La versión {newVersion} es anterior a la del pack, {Version}.");
        }

        if (string.IsNullOrWhiteSpace(changelog))
        {
            return Error.Validation("pack.changelog_empty", "Una versión nueva necesita changelog.");
        }

        if (_files.Count == 0)
        {
            return Error.Conflict("pack.no_files", "No se puede publicar un pack sin ficheros.");
        }

        Version = newVersion;
        Changelog = changelog.Trim();
        RegulatoryCheckDate = regulatoryCheckDate;
        Status = PublicationStatus.Published;
        UpdatedAt = now;
        return Unit.Value;
    }

    public long TotalSizeInBytes => _files.Sum(f => f.SizeInBytes);

    private static bool IsSemanticVersion(string version)
    {
        var parts = version.Split('.');
        return parts.Length == 3 && parts.All(p => int.TryParse(p, out var n) && n >= 0);
    }

    private static int CompareVersions(string a, string b)
    {
        var pa = a.Split('.').Select(int.Parse).ToArray();
        var pb = b.Split('.').Select(int.Parse).ToArray();

        for (var i = 0; i < 3; i++)
        {
            var cmp = pa[i].CompareTo(pb[i]);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return 0;
    }
}
