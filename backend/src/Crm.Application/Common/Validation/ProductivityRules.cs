namespace Crm.Application.Common.Validation;

public static class AgentTaskRules
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const int SearchMaxLength = 100;
    public const int DashboardPreviewLimit = 5;
    public static readonly TimeSpan DueSoonWindow = TimeSpan.FromDays(7);
}

public static class TaskReminderRules
{
    /// <summary>Issued this long after the scheduled time ⇒ labelled "missed" = delayed issuance (no check reached the server in time).
    /// It does NOT prove the Agent was offline (F6).</summary>
    public static readonly TimeSpan MissedThreshold = TimeSpan.FromMinutes(5);
}

public static class QuickReplyRules
{
    public const int TitleMaxLength = 120;
    public const int BodyMaxLength = 2000;
    public const int SearchMaxLength = 100;
}

public static class CollaborationRules
{
    public const int MessageMaxLength = 2000;
}
