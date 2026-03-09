using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace DoorMarket.Api.Security;

public static class UploadSecurityValidator
{
    public const long DefaultMaxBytes = 10_000_000;
    private const int MaxFileNameLength = 180;
    private static readonly Regex UnsafeFileNameChars = new(@"[^A-Za-z0-9._-]+", RegexOptions.Compiled);

    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/pjpeg",
        "image/webp"
    };

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    private static readonly HashSet<string> AllowedDocumentContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/pjpeg",
        "image/webp"
    };

    private static readonly HashSet<string> AllowedDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    public static Task<ValidatedUpload> ValidateImageAsync(IFormFile file, CancellationToken ct, long maxBytes = DefaultMaxBytes)
        => ValidateAsync(file, maxBytes, AllowedImageContentTypes, AllowedImageExtensions, ct);

    public static Task<ValidatedUpload> ValidateDocumentAsync(IFormFile file, CancellationToken ct, long maxBytes = DefaultMaxBytes)
        => ValidateAsync(file, maxBytes, AllowedDocumentContentTypes, AllowedDocumentExtensions, ct);

    private static async Task<ValidatedUpload> ValidateAsync(
        IFormFile file,
        long maxBytes,
        HashSet<string> allowedContentTypes,
        HashSet<string> allowedExtensions,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new InvalidOperationException("Fichier vide.");
        }

        if (file.Length > maxBytes)
        {
            throw new InvalidOperationException($"Fichier trop volumineux (max {maxBytes / 1_000_000} MB).");
        }

        var normalizedContentType = (file.ContentType ?? string.Empty).Trim().ToLowerInvariant();
        if (!allowedContentTypes.Contains(normalizedContentType))
        {
            throw new InvalidOperationException("Type de fichier non supporte.");
        }

        var normalizedFileName = NormalizeFileName(file.FileName);
        var extension = Path.GetExtension(normalizedFileName);
        if (!allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Extension de fichier non supportee.");
        }

        await ValidateMagicBytesAsync(file, extension, ct);
        return new ValidatedUpload(normalizedFileName, normalizedContentType, extension, file.Length);
    }

    private static string NormalizeFileName(string? source)
    {
        var baseName = Path.GetFileName(source ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(baseName))
        {
            throw new InvalidOperationException("Nom de fichier invalide.");
        }

        var ext = Path.GetExtension(baseName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext))
        {
            throw new InvalidOperationException("Extension de fichier obligatoire.");
        }

        var nameWithoutExt = Path.GetFileNameWithoutExtension(baseName);
        var safeStem = UnsafeFileNameChars.Replace(nameWithoutExt, "_").Trim('_');
        if (string.IsNullOrWhiteSpace(safeStem))
        {
            safeStem = "file";
        }

        var maxStemLength = Math.Max(1, MaxFileNameLength - ext.Length);
        if (safeStem.Length > maxStemLength)
        {
            safeStem = safeStem[..maxStemLength];
        }

        return $"{safeStem}{ext}";
    }

    private static async Task ValidateMagicBytesAsync(IFormFile file, string extension, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), ct);
        if (read <= 0)
        {
            throw new InvalidOperationException("Lecture fichier impossible.");
        }

        if (!HasValidSignature(extension, header, read))
        {
            throw new InvalidOperationException("Signature fichier invalide.");
        }
    }

    private static bool HasValidSignature(string extension, byte[] header, int read)
    {
        return extension.ToLowerInvariant() switch
        {
            ".png" => StartsWith(header, read, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
            ".jpg" or ".jpeg" => StartsWith(header, read, 0xFF, 0xD8, 0xFF),
            ".webp" => StartsWith(header, read, 0x52, 0x49, 0x46, 0x46) &&
                       read >= 12 &&
                       header[8] == 0x57 &&
                       header[9] == 0x45 &&
                       header[10] == 0x42 &&
                       header[11] == 0x50,
            ".pdf" => StartsWith(header, read, 0x25, 0x50, 0x44, 0x46),
            _ => false
        };
    }

    private static bool StartsWith(byte[] header, int read, params byte[] expected)
    {
        if (read < expected.Length)
        {
            return false;
        }

        for (var i = 0; i < expected.Length; i++)
        {
            if (header[i] != expected[i])
            {
                return false;
            }
        }

        return true;
    }

    public sealed record ValidatedUpload(string FileName, string ContentType, string Extension, long SizeBytes);
}
