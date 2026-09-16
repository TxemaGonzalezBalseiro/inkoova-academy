using Inkoova.Academy.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Inkoova.Academy.Infrastructure.Content;

public sealed record ContentStorageOptions
{
    /// <summary>Absolute path of the private volume that holds imported content (ADR-003).</summary>
    public required string RootPath { get; init; }
}

/// <summary>
/// Serves imported content from a private volume. Every path is resolved and checked to be
/// inside the root: a content ref is data that ultimately came from a manifest, so traversal
/// has to be impossible by construction, not by convention.
/// </summary>
public sealed class LocalDiskContentStorage(
    ContentStorageOptions options,
    ILogger<LocalDiskContentStorage> logger) : IContentStorage
{
    public Task<bool> ExistsAsync(string contentRef, CancellationToken ct) =>
        Task.FromResult(TryResolve(contentRef, out var full) && File.Exists(full));

    public Task<Stream?> OpenReadAsync(string contentRef, CancellationToken ct)
    {
        if (!TryResolve(contentRef, out var full) || !File.Exists(full))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            full, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    public Task<long> GetSizeAsync(string contentRef, CancellationToken ct) =>
        Task.FromResult(TryResolve(contentRef, out var full) && File.Exists(full)
            ? new FileInfo(full).Length
            : 0L);

    public async Task WriteAsync(string contentRef, Stream content, CancellationToken ct)
    {
        if (!TryResolve(contentRef, out var full))
        {
            throw new InvalidOperationException($"Ruta de contenido fuera del volumen permitido: {contentRef}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        await using var file = new FileStream(
            full, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 64 * 1024, useAsync: true);

        await content.CopyToAsync(file, ct);
    }

    public string GetContentType(string contentRef) =>
        Path.GetExtension(StripFragment(contentRef)).ToLowerInvariant() switch
    {
        ".html" or ".htm" => "text/html; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".js" or ".mjs" => "text/javascript; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".md" => "text/markdown; charset=utf-8",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".woff2" => "font/woff2",
        ".woff" => "font/woff",
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".zip" => "application/zip",
        // Unknown extensions are served as a download, never sniffed by the browser.
        _ => "application/octet-stream"
    };

    /// <summary>
    /// Un curso que vive en un único HTML referencia cada lección con un ancla
    /// (<c>curso/index.html#pane-m0-l0</c>). El fragmento es del navegador, no del sistema
    /// de ficheros: aquí se descarta antes de resolver la ruta.
    /// </summary>
    internal static string StripFragment(string contentRef)
    {
        var hash = contentRef.IndexOf('#', StringComparison.Ordinal);
        return hash < 0 ? contentRef : contentRef[..hash];
    }

    private bool TryResolve(string contentRef, out string fullPath)
    {
        fullPath = string.Empty;
        contentRef = StripFragment(contentRef);

        if (string.IsNullOrWhiteSpace(contentRef) || Path.IsPathRooted(contentRef))
        {
            return false;
        }

        var root = Path.GetFullPath(options.RootPath);
        var candidate = Path.GetFullPath(Path.Combine(root, contentRef.Replace('\\', '/')));

        // GetFullPath collapses "..", so comparing prefixes here is a real containment check.
        if (!candidate.StartsWith(root, StringComparison.Ordinal))
        {
            logger.LogWarning("Blocked content path outside the volume: {ContentRef}", contentRef);
            return false;
        }

        fullPath = candidate;
        return true;
    }
}
