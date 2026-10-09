using Crm.Infrastructure.Authentication;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class SessionConfiguration : IEntityTypeConfiguration<AuthenticationSession>
{
    public void Configure(EntityTypeBuilder<AuthenticationSession> session)
    {
        session.ToTable("AuthenticationSessions", "dbo", table => { table.HasCheckConstraint("CK_Sessions_Expiry", "[ExpiresAtUtc]>[CreatedAtUtc]"); table.HasCheckConstraint("CK_Sessions_Version", "[AuthVersionAtCreation]>0"); table.HasCheckConstraint("CK_Sessions_Reason", "[RevocationReason] IS NULL OR [RevocationReason] IN (0,1,2,3)"); });
        session.HasKey(x => x.Id); session.Property(x => x.RowVersion).IsRowVersion();
        session.HasOne<LocalUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        session.HasIndex(x => x.UserId); session.HasIndex(x => new { x.Id, x.UserId });
    }
}
public sealed class RefreshConfiguration : IEntityTypeConfiguration<RefreshCredential>
{
    public void Configure(EntityTypeBuilder<RefreshCredential> credential)
    {
        credential.ToTable("RefreshCredentials", "dbo", table => table.HasCheckConstraint("CK_Refresh_Expiry", "[ExpiresAtUtc]>[CreatedAtUtc]"));
        credential.HasKey(x => x.Id); credential.Property(x => x.TokenHash).IsRequired().HasColumnType("binary(32)"); credential.Property(x => x.RowVersion).IsRowVersion();
        credential.HasIndex(x => x.TokenHash).IsUnique(); credential.HasIndex(x => x.SessionId).IsUnique().HasFilter("[ConsumedAtUtc] IS NULL");
        // Lookup of all family generations must include consumed predecessors as well.
        credential.HasIndex(x => x.SessionId, "SessionLookup").HasDatabaseName("IX_RefreshCredentials_SessionLookup");
        credential.HasOne<AuthenticationSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        credential.HasOne<RefreshCredential>().WithMany().HasForeignKey(x => x.ReplacedById).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class AdminSetupConfiguration : IEntityTypeConfiguration<InitialAdminSetup>
{
    public void Configure(EntityTypeBuilder<InitialAdminSetup> setup)
    {
        setup.ToTable("InitialAdminSetup", "dbo", table => table.HasCheckConstraint("CK_AdminSetup_Singleton", "[Id]=1"));
        setup.HasKey(x => x.Id); setup.Property(x => x.Id).ValueGeneratedNever();
        setup.HasIndex(x => x.AdminUserId).IsUnique(); setup.HasOne<LocalUser>().WithMany().HasForeignKey(x => x.AdminUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
