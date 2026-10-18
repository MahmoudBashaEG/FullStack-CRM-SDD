namespace Crm.Domain.AgentTasks;

/// <summary>Stored as the member name. Archive is a separate flag, not a status.</summary>
public enum AgentTaskStatus
{
    Pending = 0,
    Completed = 1,
}
