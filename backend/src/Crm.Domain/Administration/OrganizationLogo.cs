namespace Crm.Domain.Administration;

/// <summary>PLAT-001 D8: the one organization logo (Id 1). Bytes only; metadata and audit are on OrganizationSettings.
/// ContentType comes from the server signature allow-list, never from the client. Never shares storage with customer attachments.</summary>
public sealed class OrganizationLogo
{
    public const int SingletonId = 1;

    private OrganizationLogo() { } // EF Core

    public int Id { get; private set; }
    public byte[] Content { get; private set; } = [];
    public string ContentType { get; private set; } = string.Empty;

    public static OrganizationLogo Create(byte[] content, string contentType) =>
        new() { Id = SingletonId, Content = content, ContentType = contentType };

    public void Replace(byte[] content, string contentType)
    {
        Content = content;
        ContentType = contentType;
    }
}
