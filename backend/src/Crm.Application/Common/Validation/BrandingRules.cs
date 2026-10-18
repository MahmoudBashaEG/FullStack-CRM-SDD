namespace Crm.Application.Common.Validation;

/// <summary>PLAT-001 D8/D10. Enforced on the server; the Angular checks are a usability aid only.</summary>
public static class BrandingRules
{
    public const string DefaultAccent = "violet";
    public static readonly IReadOnlyList<string> AccentPresets = [DefaultAccent, "blue", "azure", "green", "orange", "rose"];
    public const int LogoMaxBytes = 512 * 1024;
    public const long LogoRequestSizeLimitBytes = 1024 * 1024; // above LogoMaxBytes so slightly larger files get the friendly message
    public const string LogoAllowedTypesText = "PNG, JPEG or WebP";

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] Webp = "WEBP"u8.ToArray();

    public static bool IsAccentPreset(string? value) => value is null || AccentPresets.Contains(value, StringComparer.Ordinal);

    /// <summary>Stored form: the default preset is stored as null (= not configured), so choosing it is not an audited change from an unset value.</summary>
    public static string? ToStoredAccent(string? value) => value is null or DefaultAccent ? null : value;

    /// <summary>Content type from the magic bytes; null when not PNG/JPEG/WebP.</summary>
    public static string? DetectImageType(ReadOnlySpan<byte> content) =>
        content.StartsWith(Png) ? "image/png"
        : content.StartsWith(Jpeg) ? "image/jpeg"
        : content.Length >= 12 && content.StartsWith(Riff) && content[8..12].SequenceEqual(Webp) ? "image/webp"
        : null;

    /// <summary>First problem with the upload, or null. The extension must agree with the signature.</summary>
    public static string? CheckLogo(string? fileName, byte[] content)
    {
        if (content.Length == 0)
        {
            return "Select an image to upload.";
        }

        if (content.Length > LogoMaxBytes)
        {
            return "The logo is larger than 512 KB.";
        }

        var extension = Path.GetExtension(CustomerAttachmentRules.SanitizeFileName(fileName)).ToLowerInvariant();
        var expected = extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => null,
        };
        if (expected is null)
        {
            return $"Upload a {LogoAllowedTypesText} image.";
        }

        return DetectImageType(content) == expected ? null : "The file content is not a valid PNG, JPEG or WebP image.";
    }
}
