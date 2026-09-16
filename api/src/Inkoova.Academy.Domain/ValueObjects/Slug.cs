using System.Text;
using System.Text.RegularExpressions;
using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Domain.ValueObjects;

/// <summary>
/// URL segment for courses, lessons, packs and programs. Lowercase ASCII, digits and
/// single hyphens. Stable once published: changing it breaks links and certificates.
/// </summary>
public sealed partial record Slug
{
    public const int MaxLength = 120;

    public string Value { get; }

    private Slug(string value) => Value = value;

    public static Result<Slug, Error> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Error.Validation("slug.empty", "El slug no puede estar vacío.");
        }

        var trimmed = raw.Trim();

        if (trimmed.Length > MaxLength)
        {
            return Error.Validation("slug.too_long", $"El slug supera {MaxLength} caracteres.");
        }

        if (!SlugPattern().IsMatch(trimmed))
        {
            return Error.Validation(
                "slug.invalid",
                "El slug solo admite minúsculas, dígitos y guiones simples, sin guion inicial ni final.");
        }

        return new Slug(trimmed);
    }

    /// <summary>
    /// Best-effort slug from a human title. Used by the importer, never by user input:
    /// a title that produces an empty slug is a content bug and must fail loudly.
    /// </summary>
    public static Result<Slug, Error> FromTitle(string title)
    {
        var normalized = title.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
            else if (ch is ' ' or '-' or '_' or '.' or '/')
            {
                sb.Append('-');
            }
        }

        var collapsed = CollapseHyphens().Replace(sb.ToString(), "-").Trim('-');

        if (collapsed.Length > MaxLength)
        {
            collapsed = collapsed[..MaxLength].TrimEnd('-');
        }

        return Create(collapsed);
    }

    public override string ToString() => Value;

    public static implicit operator string(Slug slug) => slug.Value;

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    [GeneratedRegex("-{2,}")]
    private static partial Regex CollapseHyphens();
}
