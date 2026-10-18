using Crm.Application.Common.Exceptions;
using FluentValidation;
using FluentValidation.Results;

namespace Crm.Api.Http;

/// <summary>Strong ETag = the quoted base64 rowversion. Only the exact single value the API returned is accepted.</summary>
internal static class EntityTags
{
    private const int RowVersionLength = 8; // SQL Server rowversion is always 8 bytes

    public static string Format(byte[] version) => $"\"{Convert.ToBase64String(version)}\"";

    /// <summary>
    /// Missing ⇒ 428. '*', weak (W/…), several values, non-base64 or not exactly 8 decoded bytes ⇒ 400 keyed "ifMatch".
    /// A well-formed 8-byte value that is not the current version passes here; the handler then returns 409 (stale).
    /// </summary>
    public static byte[] RequireIfMatch(HttpRequest request)
    {
        var header = request.Headers.IfMatch;
        var raw = header.ToString().Trim();
        if (header.Count == 0 || raw.Length == 0)
        {
            throw new PreconditionRequiredException("Send If-Match with the version you last loaded.");
        }

        if (header.Count == 1 && raw.Length > 2 && raw[0] == '"' && raw[^1] == '"' && !raw.Contains(','))
        {
            try
            {
                var version = Convert.FromBase64String(raw[1..^1]);
                if (version.Length == RowVersionLength)
                {
                    return version;
                }
            }
            catch (FormatException)
            {
                // fall through to 400
            }
        }

        throw new ValidationException([new ValidationFailure("IfMatch", "If-Match must be the quoted version returned by the API.")]);
    }
}
