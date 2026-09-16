using System.Reflection;
using FluentAssertions;
using Inkoova.Academy.Domain.Catalog;

namespace Inkoova.Academy.Domain.Tests;

/// <summary>
/// Guards the layering rule from CLAUDE.md convention 4. A reference added by accident —
/// a Dapper attribute, an ASP.NET type in a signature — is caught here rather than during a
/// refactor months later.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Course).Assembly;

    [Fact]
    public void The_domain_references_nothing_but_the_base_class_library()
    {
        var forbidden = Domain
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name =>
                name.StartsWith("Dapper", StringComparison.Ordinal)
                || name.StartsWith("Npgsql", StringComparison.Ordinal)
                || name.StartsWith("Stripe", StringComparison.Ordinal)
                || name.StartsWith("QuestPDF", StringComparison.Ordinal)
                || name.StartsWith("MailKit", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.Extensions", StringComparison.Ordinal))
            .ToList();

        forbidden.Should().BeEmpty(
            "el dominio no puede depender de infraestructura ni de ASP.NET (CLAUDE.md, convención 4)");
    }

    [Fact]
    public void The_domain_does_not_reference_the_application_layer()
    {
        Domain.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Should().NotContain("Inkoova.Academy.Application");
    }

    [Fact]
    public void Every_public_domain_entity_hides_its_constructors()
    {
        // Entities are created through factories that enforce invariants; a public
        // constructor would be a way around them.
        var entities = Domain.GetTypes()
            .Where(t => t is { IsClass: true, IsPublic: true, IsAbstract: false })
            .Where(t => t.Namespace is not null && !t.Namespace.EndsWith("Common", StringComparison.Ordinal))
            .Where(t => !t.Name.EndsWith("Exception", StringComparison.Ordinal))
            // Records generate a public constructor by design; they are value objects here.
            .Where(t => t.GetMethod("<Clone>$") is null)
            .ToList();

        var offenders = entities
            .Where(t => t.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length > 0)
            .Select(t => t.Name)
            .ToList();

        offenders.Should().BeEmpty("las entidades se crean por factoría para garantizar sus invariantes");
    }
}
