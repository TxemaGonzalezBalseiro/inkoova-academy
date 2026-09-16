using FluentAssertions;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Tests;

/// <summary>
/// Publicar un pack.
///
/// La regla que gobierna esto —«una versión nueva tiene que ser posterior»— está para que
/// publicar avise a quien ya descargó una versión anterior. Vale para las actualizaciones y
/// estorba en la primera publicación, que es lo que fijan estos tests.
/// </summary>
public class PackReleaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Verificacion = new(2026, 9, 1);

    [Fact]
    public void The_first_release_can_keep_the_version_the_pack_was_created_with()
    {
        var pack = ConFichero();

        // Nace en 1.0.0 y sin publicar. Exigirle 1.0.1 obligaría a estrenar saltándose la
        // primera versión, que es mentir sobre el historial.
        pack.Version.Should().Be("1.0.0");

        pack.ReleaseVersion("1.0.0", "Primera versión.", Verificacion, Now)
            .IsSuccess.Should().BeTrue();

        pack.Status.Should().Be(PublicationStatus.Published);
        pack.RegulatoryCheckDate.Should().Be(Verificacion);
    }

    [Fact]
    public void Once_published_the_same_version_cannot_be_released_again()
    {
        var pack = ConFichero();
        pack.ReleaseVersion("1.0.0", "Primera versión.", Verificacion, Now);

        // Ya publicado: republicar la misma versión avisaría por correo a quien la descargó de
        // un cambio que no existe.
        var otra = pack.ReleaseVersion("1.0.0", "Otra vez.", Verificacion, Now);

        otra.IsFailure.Should().BeTrue();
        otra.Error.Code.Should().Be("pack.version_not_greater");
    }

    [Fact]
    public void A_published_pack_moves_forward_but_never_back()
    {
        var pack = ConFichero();
        pack.ReleaseVersion("1.0.0", "Primera versión.", Verificacion, Now);

        pack.ReleaseVersion("1.1.0", "Se actualiza la clasificación.", Verificacion, Now)
            .IsSuccess.Should().BeTrue();

        pack.ReleaseVersion("1.0.5", "Retroceso.", Verificacion, Now)
            .Error.Code.Should().Be("pack.version_not_greater");

        pack.Version.Should().Be("1.1.0");
    }

    [Fact]
    public void Not_even_the_first_release_can_go_below_the_version_of_the_catalogue()
    {
        var pack = ConFichero();

        // Publicar por debajo dejaría el catálogo diciendo una cosa y el pack otra.
        pack.ReleaseVersion("0.9.0", "Anterior.", Verificacion, Now)
            .Error.Code.Should().Be("pack.version_not_greater");
    }

    [Fact]
    public void A_pack_without_files_is_not_published()
    {
        var pack = Nuevo();

        // Publicar sin ficheros deja un pack que se vende y no se puede descargar.
        pack.ReleaseVersion("1.0.0", "Primera versión.", Verificacion, Now)
            .Error.Code.Should().Be("pack.no_files");
    }

    [Fact]
    public void A_release_needs_a_changelog_because_it_goes_in_the_email()
    {
        var pack = ConFichero();

        pack.ReleaseVersion("1.0.0", "   ", Verificacion, Now)
            .Error.Code.Should().Be("pack.changelog_empty");
    }

    private static Pack Nuevo() =>
        Pack.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(),
            // Un slug por pack: los tests comparten proceso y dos con el mismo chocarían.
            Slug.Create($"pack-{Guid.CreateVersion7():N}").Value,
            "Pack de prueba", "Sector de prueba", "1.0.0", Now).Value;

    private static Pack ConFichero()
    {
        var pack = Nuevo();

        pack.AddFile(new PackFile(
            Guid.CreateVersion7(), pack.Id, "checklist.docx",
            "packs/prueba/checklist.docx", 1024, new string('a', 64), 0));

        return pack;
    }
}
