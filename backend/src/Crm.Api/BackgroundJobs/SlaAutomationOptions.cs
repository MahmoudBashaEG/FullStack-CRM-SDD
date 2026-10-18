namespace Crm.Api.BackgroundJobs;

/// <summary>Section "SlaAutomation". Single API instance only (SLA-001 §7) — several replicas would need coordination.</summary>
public sealed class SlaAutomationOptions
{
    public const string SectionName = "SlaAutomation";

    public bool Enabled { get; set; } = true;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(60);
}
