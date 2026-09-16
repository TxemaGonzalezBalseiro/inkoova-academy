using FluentAssertions;
using Inkoova.Academy.Domain.Learning;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Tests;

public class CertificateTests
{
    private const string Secret = "test-signing-secret-not-used-in-production";
    private static readonly DateTimeOffset IssuedAt = new(2026, 8, 30, 10, 15, 0, TimeSpan.Zero);

    [Fact]
    public void A_certificate_is_not_issued_before_every_required_lesson_is_complete()
    {
        var required = new HashSet<Guid> { Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7() };
        var completed = required.Take(2).ToHashSet();

        var result = Certificate.Issue(
            Guid.CreateVersion7(), CertificateScope.Course, Guid.CreateVersion7(), Guid.CreateVersion7(),
            required, completed, IssuedAt, Secret);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("certificate.incomplete");
        result.Error.Message.Should().Contain("1");
    }

    [Fact]
    public void A_certificate_is_issued_when_all_required_lessons_are_complete()
    {
        var required = new HashSet<Guid> { Guid.CreateVersion7(), Guid.CreateVersion7() };

        var result = Certificate.Issue(
            Guid.CreateVersion7(), CertificateScope.Course, Guid.CreateVersion7(), Guid.CreateVersion7(),
            required, required, IssuedAt, Secret);

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Value.Should().MatchRegex("^INK-[A-Z0-9]{4}-[A-Z0-9]{4}$");
        result.Value.IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_course_without_required_lessons_cannot_produce_a_certificate()
    {
        var result = Certificate.Issue(
            Guid.CreateVersion7(), CertificateScope.Course, Guid.CreateVersion7(), Guid.CreateVersion7(),
            new HashSet<Guid>(), new HashSet<Guid>(), IssuedAt, Secret);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("certificate.no_required_lessons");
    }

    [Fact]
    public void The_hash_is_reproducible_from_the_same_inputs()
    {
        var userId = Guid.CreateVersion7();
        var subjectId = Guid.CreateVersion7();

        var first = Certificate.ComputeHash(userId, subjectId, IssuedAt, Secret);
        var second = Certificate.ComputeHash(userId, subjectId, IssuedAt, Secret);

        second.Should().Be(first);
    }

    [Fact]
    public void The_hash_ignores_sub_second_precision_so_a_database_round_trip_still_verifies()
    {
        var userId = Guid.CreateVersion7();
        var subjectId = Guid.CreateVersion7();

        var exact = Certificate.ComputeHash(userId, subjectId, IssuedAt, Secret);
        var withMicroseconds = Certificate.ComputeHash(userId, subjectId, IssuedAt.AddTicks(4231), Secret);

        withMicroseconds.Should().Be(exact);
    }

    [Fact]
    public void A_different_secret_produces_a_different_hash()
    {
        var userId = Guid.CreateVersion7();
        var subjectId = Guid.CreateVersion7();

        var mine = Certificate.ComputeHash(userId, subjectId, IssuedAt, Secret);
        var forged = Certificate.ComputeHash(userId, subjectId, IssuedAt, "otro-secreto");

        forged.Should().NotBe(mine);
    }

    [Fact]
    public void Verification_fails_when_the_stored_hash_does_not_match_the_secret()
    {
        var required = new HashSet<Guid> { Guid.CreateVersion7() };
        var certificate = Certificate.Issue(
            Guid.CreateVersion7(), CertificateScope.Course, Guid.CreateVersion7(), Guid.CreateVersion7(),
            required, required, IssuedAt, Secret).Value;

        certificate.Verify(Secret).Should().BeTrue();
        certificate.Verify("otro-secreto").Should().BeFalse();
    }

    [Fact]
    public void Revocation_requires_a_reason_and_cannot_be_repeated()
    {
        var required = new HashSet<Guid> { Guid.CreateVersion7() };
        var certificate = Certificate.Issue(
            Guid.CreateVersion7(), CertificateScope.Course, Guid.CreateVersion7(), Guid.CreateVersion7(),
            required, required, IssuedAt, Secret).Value;

        certificate.Revoke(IssuedAt, "  ").IsFailure.Should().BeTrue();

        certificate.Revoke(IssuedAt, "fraude").IsSuccess.Should().BeTrue();
        certificate.IsValid.Should().BeFalse();

        certificate.Revoke(IssuedAt, "otra vez").Error.Code.Should().Be("certificate.already_revoked");
    }

    [Fact]
    public void The_code_alphabet_excludes_characters_that_are_misread_when_dictated()
    {
        // Only the random half is checked: the INK- prefix is fixed branding.
        var randomParts = Enumerable.Range(0, 200)
            .Select(_ => CertificateCode.NewCode().Value["INK-".Length..].Replace("-", string.Empty))
            .ToList();

        randomParts.Should().AllSatisfy(part =>
            part.Should().NotContainAny("I", "L", "O", "U", "0", "1"));
    }
}
