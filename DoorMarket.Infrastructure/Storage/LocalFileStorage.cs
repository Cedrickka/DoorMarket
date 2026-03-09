using DoorMarket.Application.Interfaces.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace DoorMarket.Infrastructure.Storage;

public sealed class LocalFileStorage : IFileStorage
{
    private static readonly Regex UnsafePathSegmentChars = new(@"[^A-Za-z0-9/_-]+", RegexOptions.Compiled);
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _cfg;

    public LocalFileStorage(IWebHostEnvironment env, IConfiguration cfg)
    {
        _env = env;
        _cfg = cfg;
    }

    public async Task<(string storagePath, string publicUrl, long sizeBytes)> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken ct)
    {
        var root = _cfg["Uploads:Root"] ?? "uploads";
        var safeFolder = NormalizeRelativeFolder(folder);
        var safeExtension = NormalizeExtension(fileName);
        var safeName = $"{Guid.NewGuid():N}{safeExtension}";

        // IMPORTANT: wwwroot doit exister cote API
        var webRoot = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        Directory.CreateDirectory(webRoot);

        var relFolder = Path.Combine(root, safeFolder).Replace("\\", "/");
        var absFolder = Path.Combine(webRoot, relFolder);
        Directory.CreateDirectory(absFolder);

        var relPath = Path.Combine(relFolder, safeName).Replace("\\", "/");
        var absPath = Path.Combine(webRoot, relPath);

        await using var fs = File.Create(absPath);
        await content.CopyToAsync(fs, ct);

        var len = new FileInfo(absPath).Length;

        // PublicBaseUrl: ex https://api.doormarket.com
        var baseUrl = NormalizePublicBaseUrl(_cfg["Uploads:PublicBaseUrl"]);
        var publicUrl = string.IsNullOrWhiteSpace(baseUrl) ? $"/{relPath}" : $"{baseUrl}/{relPath}";

        return (relPath, publicUrl, len);
    }

    private static string NormalizeRelativeFolder(string? folder)
    {
        var raw = (folder ?? string.Empty).Replace("\\", "/").Trim('/');
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "misc";
        }

        var withoutTraversal = raw.Replace("..", string.Empty, StringComparison.Ordinal);
        var normalized = UnsafePathSegmentChars.Replace(withoutTraversal, string.Empty).Trim('/');
        return string.IsNullOrWhiteSpace(normalized) ? "misc" : normalized;
    }

    private static string NormalizeExtension(string? fileName)
    {
        var ext = Path.GetExtension(Path.GetFileName(fileName ?? string.Empty)).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext))
        {
            return ".bin";
        }

        return ext.Length <= 12 ? ext : ".bin";
    }

    private static string NormalizePublicBaseUrl(string? configuredBaseUrl)
    {
        var value = (configuredBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var isLocalhost =
            value.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("127.0.0.1", StringComparison.OrdinalIgnoreCase);

        return isLocalhost ? $"http://{value}" : $"https://{value}";
    }
}
