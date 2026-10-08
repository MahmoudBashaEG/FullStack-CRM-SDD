namespace Crm.Application.Tickets.Contracts;

public static class EditVersion
{
    public static bool TryParse(string? text, out byte[] bytes)
    {
        bytes = [];
        if (text is null) return false;
        try { bytes = Convert.FromBase64String(text); return bytes.Length == 8 && Convert.ToBase64String(bytes) == text; }
        catch (FormatException) { return false; }
    }
}
