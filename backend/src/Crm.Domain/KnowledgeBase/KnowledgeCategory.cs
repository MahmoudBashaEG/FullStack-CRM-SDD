namespace Crm.Domain.KnowledgeBase;

/// <summary>Flat SuperAdmin-maintained grouping (KB-001 §5). Never deleted: deactivate instead; existing articles keep it.</summary>
public sealed class KnowledgeCategory
{
    private KnowledgeCategory() { } // EF Core

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static KnowledgeCategory Create(Guid id, string name, Guid superAdminUserId, DateTimeOffset now) =>
        new()
        {
            Id = id, Name = name, IsActive = true, CreatedByUserId = superAdminUserId, UpdatedByUserId = superAdminUserId,
            CreatedAtUtc = now, UpdatedAtUtc = now,
        };

    public bool Update(string name, bool isActive, Guid superAdminUserId, DateTimeOffset now)
    {
        if (string.Equals(Name, name, StringComparison.Ordinal) && IsActive == isActive)
        {
            return false;
        }

        Name = name;
        IsActive = isActive;
        UpdatedByUserId = superAdminUserId;
        UpdatedAtUtc = now;
        return true;
    }
}
