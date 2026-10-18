namespace Crm.Application.Common.Validation;

public static class KnowledgeBaseRules
{
    public const int TitleMaxLength = 200;
    public const int SummaryMaxLength = 500;
    public const int BodyMaxLength = 20_000;
    public const int CategoryNameMaxLength = 100;
    public const int ReturnReasonMaxLength = 500;
    public const int SearchMaxLength = 100;
    /// <summary>Keeps the unpaged ticket link list small (KB-001 §10).</summary>
    public const int MaxLinksPerTicket = 25;
}
