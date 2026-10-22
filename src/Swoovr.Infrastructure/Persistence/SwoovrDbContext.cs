using System.Text;
using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;

namespace Swoovr.Infrastructure.Persistence;

/// <summary>
/// The application database (DEFAULT PostgreSQL). Holds what Keycloak does not (D55):
/// accounts keyed by (issuer, subject), roles, invitations, profile flags, age details and
/// the audit log. Names are snake_case; enums are stored as their names so a reordered enum
/// can never change stored meaning.
/// </summary>
public sealed class SwoovrDbContext(DbContextOptions<SwoovrDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<RoleGrant> RoleGrants => Set<RoleGrant>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Account>(account =>
        {
            account.HasIndex(a => new { a.Issuer, a.Subject }).IsUnique();
            // One live account per verified address within a realm; never used to merge (D38).
            account.HasIndex(a => new { a.Realm, a.Email });
            account.Property(a => a.Issuer).HasMaxLength(512);
            account.Property(a => a.Subject).HasMaxLength(255);
            account.Property(a => a.Email).HasMaxLength(254);
            account.Property(a => a.StatusReason).HasMaxLength(64);
        });
        model.Entity<RoleGrant>(grant =>
        {
            grant.HasOne<Account>().WithMany().HasForeignKey(g => g.AccountId).OnDelete(DeleteBehavior.Cascade);
            grant.HasIndex(g => new { g.AccountId, g.Role, g.VenueId }).IsUnique().AreNullsDistinct(false);
        });
        model.Entity<Invitation>(invitation =>
        {
            invitation.Property(i => i.Email).HasMaxLength(254);
            invitation.HasIndex(i => new { i.Realm, i.Email });
        });
        model.Entity<MemberProfile>(profile =>
        {
            profile.HasKey(p => p.AccountId);
            profile.HasOne<Account>().WithOne().HasForeignKey<MemberProfile>(p => p.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<AuditEntry>(audit =>
        {
            audit.Property(a => a.Actor).HasMaxLength(16);
            audit.Property(a => a.Action).HasMaxLength(64);
            audit.Property(a => a.TargetType).HasMaxLength(32);
            audit.Property(a => a.TargetId).HasMaxLength(64);
            audit.Property(a => a.Detail).HasMaxLength(512);
            audit.HasIndex(a => new { a.ActorAccountId, a.Action, a.At });
        });

        foreach (var entity in model.Model.GetEntityTypes())
        {
            entity.SetTableName(Snake(entity.GetTableName()!));
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(Snake(property.Name));
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (type.IsEnum) property.SetProviderClrType(typeof(string));
                if (type.IsEnum) property.SetMaxLength(32);
            }
            foreach (var key in entity.GetKeys()) key.SetName(Snake(key.GetName()!));
            foreach (var foreignKey in entity.GetForeignKeys()) foreignKey.SetConstraintName(Snake(foreignKey.GetConstraintName()!));
            foreach (var index in entity.GetIndexes()) index.SetDatabaseName(Snake(index.GetDatabaseName()!));
        }
    }

    private static string Snake(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0 && name[i - 1] != '_' && (char.IsLower(name[i - 1]) || i + 1 < name.Length && char.IsLower(name[i + 1])))
                builder.Append('_');
            builder.Append(char.ToLowerInvariant(c));
        }
        return builder.ToString();
    }
}
