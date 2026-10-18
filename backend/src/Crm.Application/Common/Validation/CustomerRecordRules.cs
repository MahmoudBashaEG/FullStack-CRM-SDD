namespace Crm.Application.Common.Validation;

public static class CustomerRecordRules
{
    public const int NoteMaxLength = 2000;
    public const int InteractionSummaryMaxLength = 500;
    public static readonly TimeSpan InteractionFutureTolerance = TimeSpan.FromMinutes(5); // client clock skew
    public static readonly DateTimeOffset InteractionEarliest = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
}

/// <summary>C4 attachment rules. Enforced on the server; the Angular checks are a usability aid only.</summary>
public static class CustomerAttachmentRules
{
    public const int MaxBytes = 5 * 1024 * 1024;
    /// <summary>Whole multipart request cap: above MaxBytes so 5–6 MB files still get the friendly validation message.</summary>
    public const long RequestSizeLimitBytes = 6 * 1024 * 1024;
    public const int FileNameMaxLength = 255;
    public const string AllowedTypesText = "PDF, PNG, JPG/JPEG, TXT or DOCX";

    private static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".txt"] = "text/plain",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    };

    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04]; // DOCX is a zip package

    /// <summary>Display name only: last path segment, no control characters, trimmed. Never used as a path.</summary>
    public static string SanitizeFileName(string? fileName)
    {
        var name = (fileName ?? string.Empty).Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        return new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
    }

    /// <summary>Server content type for an already sanitized, allowed file name.</summary>
    public static string ContentTypeFor(string sanitizedFileName) => ContentTypes[Path.GetExtension(sanitizedFileName)];

    /// <summary>First problem with the upload, or null when it is acceptable.</summary>
    public static string? Check(string? fileName, byte[] content)
    {
        if (content.Length == 0)
        {
            return "Select a file to upload. Empty files are not accepted.";
        }

        if (content.Length > MaxBytes)
        {
            return "The file is larger than 5 MB.";
        }

        var name = SanitizeFileName(fileName);
        if (name.Length == 0)
        {
            return "The file must have a name.";
        }

        if (name.Length > FileNameMaxLength)
        {
            return $"The file name is too long (max {FileNameMaxLength} characters).";
        }

        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (!ContentTypes.ContainsKey(extension))
        {
            return $"This file type is not allowed. Upload a {AllowedTypesText} file.";
        }

        var signatureOk = extension switch
        {
            ".pdf" => content.AsSpan().StartsWith(Pdf),
            ".png" => content.AsSpan().StartsWith(Png),
            ".jpg" or ".jpeg" => content.AsSpan().StartsWith(Jpeg),
            ".docx" => content.AsSpan().StartsWith(Zip),
            _ => !content.AsSpan().Contains((byte)0), // .txt: plain text has no NUL bytes
        };
        return signatureOk ? null : "The file content does not match its extension.";
    }
}
