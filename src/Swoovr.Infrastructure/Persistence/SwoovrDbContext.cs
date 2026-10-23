using System.Text;
using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;

namespace Swoovr.Infrastructure.Persistence;

/// <summary>
/// The application database (DEFAULT PostgreSQL). Holds what Keycloak does not (D55):
/// accounts keyed by (issuer, subject), roles, invitations, profile flags, age details, beta
/// enrolments (D62), founder places and tokens (D65), connection modes and members' mode settings (D92)
/// and the audit log. Names are snake_case; enums are stored as their names
/// so a reordered enum can never change stored meaning.
/// </summary>
public sealed class SwoovrDbContext(DbContextOptions<SwoovrDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<RoleGrant> RoleGrants => Set<RoleGrant>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<BetaEnrolment> BetaEnrolments => Set<BetaEnrolment>();
    public DbSet<FounderPlace> FounderPlaces => Set<FounderPlace>();
    public DbSet<FounderToken> FounderTokens => Set<FounderToken>();
    public DbSet<ConnectionMode> ConnectionModes => Set<ConnectionMode>();
    public DbSet<ModeIntention> ModeIntentions => Set<ModeIntention>();
    public DbSet<MemberMode> MemberModes => Set<MemberMode>();
    public DbSet<MemberModeIntention> MemberModeIntentions => Set<MemberModeIntention>();

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
            profile.HasIndex(p => p.FounderPlace).IsUnique().HasFilter("founder_place IS NOT NULL");
            // Existing rows get one too; PostgreSQL 13+ has gen_random_uuid() built in.
            profile.Property(p => p.PublicId).HasDefaultValueSql("gen_random_uuid()");
            profile.HasIndex(p => p.PublicId).IsUnique();
            profile.Property(p => p.DisplayName).HasMaxLength(ProfileTextPolicy.DisplayNameMax);
            profile.Property(p => p.Area).HasMaxLength(ProfileTextPolicy.AreaMax);
            profile.Property(p => p.About).HasMaxLength(ProfileTextPolicy.AboutMax);
        });
        // D92: four modes with stable ids; labels, order and availability are data. Q16: forty intention chips with
        // fixed ids (IntentionChips, D113).
        model.Entity<ConnectionMode>(mode =>
        {
            mode.Property(m => m.Id).HasMaxLength(32);
            mode.Property(m => m.Label).HasMaxLength(40);
            mode.HasData(
                new ConnectionMode { Id = ConnectionModeIds.Dating, Label = "Dating", SortOrder = 1, Available = true },
                new ConnectionMode { Id = ConnectionModeIds.Friends, Label = "Friends", SortOrder = 2, Available = true },
                new ConnectionMode { Id = ConnectionModeIds.Activities, Label = "Activities", SortOrder = 3, Available = true },
                new ConnectionMode { Id = ConnectionModeIds.Business, Label = "Business", SortOrder = 4, Available = true });
        });
        model.Entity<ModeIntention>(intention =>
        {
            intention.Property(i => i.ModeId).HasMaxLength(32);
            intention.Property(i => i.Label).HasMaxLength(40);
            intention.HasOne<ConnectionMode>().WithMany().HasForeignKey(i => i.ModeId).OnDelete(DeleteBehavior.Restrict);
            intention.HasData(IntentionChips.All);
        });
        model.Entity<MemberMode>(setting =>
        {
            setting.HasKey(m => new { m.AccountId, m.ModeId });
            setting.Property(m => m.ModeId).HasMaxLength(32);
            setting.HasOne<Account>().WithMany().HasForeignKey(m => m.AccountId).OnDelete(DeleteBehavior.Cascade);
            setting.HasOne<ConnectionMode>().WithMany().HasForeignKey(m => m.ModeId).OnDelete(DeleteBehavior.Restrict);
            setting.HasIndex(m => new { m.ModeId, m.Enabled, m.Visible });
        });
        model.Entity<MemberModeIntention>(chosen =>
        {
            chosen.HasKey(m => new { m.AccountId, m.ModeId, m.IntentionId });
            chosen.Property(m => m.ModeId).HasMaxLength(32);
            chosen.HasOne<MemberMode>().WithMany().HasForeignKey(m => new { m.AccountId, m.ModeId }).OnDelete(DeleteBehavior.Cascade);
            chosen.HasOne<ModeIntention>().WithMany().HasForeignKey(m => m.IntentionId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<FounderPlace>(place =>
        {
            place.HasKey(p => p.Place);
            place.Property(p => p.Place).ValueGeneratedNever();
            place.ToTable(t => t.HasCheckConstraint("ck_founder_places_place", "place >= 1"));
        });
        // Founder tokens hold no email and no account id, so they survive account deletion (D65).
        model.Entity<FounderToken>(token =>
        {
            token.Property(t => t.Code).HasMaxLength(96);
            token.HasIndex(t => t.Code).IsUnique();
            token.HasIndex(t => t.Place);
            token.Property(t => t.InvalidatedReason).HasMaxLength(32);
            token.HasOne<FounderPlace>().WithMany().HasForeignKey(t => t.Place).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<BetaEnrolment>(enrolment =>
        {
            enrolment.Property(e => e.Email).HasMaxLength(254);
            // One live enrolment per address; a revoked one can be added again later.
            enrolment.HasIndex(e => e.Email).IsUnique().HasFilter("revoked_at IS NULL");
            enrolment.HasIndex(e => e.AccountId);
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
