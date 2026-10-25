using System.Text;
using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;

namespace Swoovr.Infrastructure.Persistence;

/// <summary>
/// The application database (DEFAULT PostgreSQL). Holds what Keycloak does not (D55):
/// accounts keyed by (issuer, subject), roles, invitations, profile flags, age details, beta
/// enrolments (D62), founder places and tokens (D65), connection modes and members' mode settings (D92), badge order
/// and bot mode authorisations (Q11), markets with their areas, modes and bot assignments (D125)
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
    public DbSet<BadgeDefinition> BadgeDefinitions => Set<BadgeDefinition>();
    public DbSet<BotModeAuthorisation> BotModeAuthorisations => Set<BotModeAuthorisation>();
    public DbSet<ConnectionRequest> ConnectionRequests => Set<ConnectionRequest>();
    public DbSet<Block> Blocks => Set<Block>();
    public DbSet<GeoCity> GeoCities => Set<GeoCity>();
    public DbSet<GeoCityName> GeoCityNames => Set<GeoCityName>();
    public DbSet<Market> Markets => Set<Market>();
    public DbSet<MarketArea> MarketAreas => Set<MarketArea>();
    public DbSet<MarketMode> MarketModes => Set<MarketMode>();
    public DbSet<BotMarket> BotMarkets => Set<BotMarket>();
    public DbSet<MarketInterest> MarketInterests => Set<MarketInterest>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Itinerary> Itineraries => Set<Itinerary>();

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
            profile.HasOne<GeoCity>().WithMany().HasForeignKey(p => p.HomeGeoNameId).OnDelete(DeleteBehavior.Restrict);
            // D146: the derived home market; a market that is removed leaves the member outside every market.
            profile.Property(p => p.HomeMarketId).HasMaxLength(32);
            profile.Property(p => p.HomeMarketMovedTo).HasMaxLength(32);
            profile.HasOne<Market>().WithMany().HasForeignKey(p => p.HomeMarketId).OnDelete(DeleteBehavior.SetNull);
            profile.HasIndex(p => p.HomeMarketId);
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
        // Q11 rule 61: badge order is data, seeded in the D74 order.
        model.Entity<BadgeDefinition>(badge =>
        {
            badge.HasKey(b => b.Badge);
            badge.HasData(BadgePolicy.DefaultOrder.Select((b, i) => new BadgeDefinition { Badge = b, SortOrder = i + 1 }));
        });
        model.Entity<BotModeAuthorisation>(authorised =>
        {
            authorised.HasKey(b => new { b.AccountId, b.ModeId });
            authorised.Property(b => b.ModeId).HasMaxLength(32);
            authorised.HasOne<Account>().WithMany().HasForeignKey(b => b.AccountId).OnDelete(DeleteBehavior.Cascade);
            authorised.HasOne<ConnectionMode>().WithMany().HasForeignKey(b => b.ModeId).OnDelete(DeleteBehavior.Restrict);
        });
        // D112: requests per pair and mode. At most one pending and one accepted per pair and mode, either direction.
        model.Entity<ConnectionRequest>(request =>
        {
            request.Property(r => r.ModeId).HasMaxLength(32);
            request.HasOne<Account>().WithMany().HasForeignKey(r => r.FromAccountId).OnDelete(DeleteBehavior.Cascade);
            request.HasOne<Account>().WithMany().HasForeignKey(r => r.ToAccountId).OnDelete(DeleteBehavior.Cascade);
            request.HasOne<ConnectionMode>().WithMany().HasForeignKey(r => r.ModeId).OnDelete(DeleteBehavior.Restrict);
            request.HasOne<Trip>().WithMany().HasForeignKey(r => r.TripId).OnDelete(DeleteBehavior.SetNull);
            request.HasIndex(r => new { r.PairLow, r.PairHigh, r.ModeId }, "ix_connection_requests_one_pending").IsUnique().HasFilter("status = 'Pending'");
            request.HasIndex(r => new { r.PairLow, r.PairHigh, r.ModeId }, "ix_connection_requests_one_accepted").IsUnique().HasFilter("status = 'Accepted'");
            request.HasIndex(r => new { r.ToAccountId, r.Status });
            request.HasIndex(r => new { r.FromAccountId, r.CreatedAt });
            request.ToTable(t => t.HasCheckConstraint("ck_connection_requests_not_self", "from_account_id <> to_account_id"));
        });
        model.Entity<Block>(block =>
        {
            block.HasKey(b => new { b.BlockerAccountId, b.BlockedAccountId });
            block.HasOne<Account>().WithMany().HasForeignKey(b => b.BlockerAccountId).OnDelete(DeleteBehavior.Cascade);
            block.HasOne<Account>().WithMany().HasForeignKey(b => b.BlockedAccountId).OnDelete(DeleteBehavior.Cascade);
            block.HasIndex(b => b.BlockedAccountId);
            block.ToTable(t => t.HasCheckConstraint("ck_blocks_not_self", "blocker_account_id <> blocked_account_id"));
        });
        // D111: GeoNames cities and their searchable spellings (prefix search on a folded key).
        model.Entity<GeoCity>(city =>
        {
            city.HasKey(c => c.GeoNameId);
            city.Property(c => c.GeoNameId).ValueGeneratedNever();
            city.Property(c => c.Name).HasMaxLength(200);
            city.Property(c => c.AsciiName).HasMaxLength(200);
            city.Property(c => c.CountryCode).HasMaxLength(2);
            city.Property(c => c.CountryName).HasMaxLength(200);
            city.Property(c => c.Admin1Code).HasMaxLength(20);
            city.Property(c => c.Admin1Name).HasMaxLength(200);
            city.Property(c => c.TimeZone).HasMaxLength(40);
        });
        model.Entity<GeoCityName>(name =>
        {
            name.HasKey(n => new { n.Key, n.GeoNameId });
            name.Property(n => n.Key).HasMaxLength(200);
            name.HasIndex(n => n.Key, "ix_geo_city_names_key_prefix").HasOperators("text_pattern_ops");
            name.HasOne<GeoCity>().WithMany().HasForeignKey(n => n.GeoNameId).OnDelete(DeleteBehavior.Cascade);
        });
        // D110, D125: markets are rows with a status, areas and modes; Adelaide (one 50 km area around GeoNames 2078025,
        // D124) is the only one, a beta market offering all four modes.
        model.Entity<Market>(market =>
        {
            market.Property(m => m.Id).HasMaxLength(32);
            market.Property(m => m.Name).HasMaxLength(80);
            market.Ignore(m => m.Open);
            market.HasData(new Market { Id = "adelaide", Name = "Adelaide", Status = MarketStatus.Beta });
        });
        model.Entity<MarketArea>(area =>
        {
            area.HasKey(a => new { a.MarketId, a.CentreGeoNameId });
            area.Property(a => a.MarketId).HasMaxLength(32);
            area.HasOne<Market>().WithMany().HasForeignKey(a => a.MarketId).OnDelete(DeleteBehavior.Cascade);
            area.ToTable(t => t.HasCheckConstraint("ck_market_areas_radius", "radius_km > 0 AND radius_km <= 1000"));
            area.HasData(new MarketArea { MarketId = "adelaide", CentreGeoNameId = 2078025, RadiusKm = 50 });
        });
        model.Entity<MarketMode>(mode =>
        {
            mode.HasKey(m => new { m.MarketId, m.ModeId });
            mode.Property(m => m.MarketId).HasMaxLength(32);
            mode.Property(m => m.ModeId).HasMaxLength(32);
            mode.HasOne<Market>().WithMany().HasForeignKey(m => m.MarketId).OnDelete(DeleteBehavior.Cascade);
            mode.HasOne<ConnectionMode>().WithMany().HasForeignKey(m => m.ModeId).OnDelete(DeleteBehavior.Restrict);
            mode.HasData(ConnectionModeIds.All.Select(id => new MarketMode { MarketId = "adelaide", ModeId = id }));
        });
        // `*` (every open market) has no market row, so this table has no foreign key to markets; the admin route checks ids.
        model.Entity<BotMarket>(bot =>
        {
            bot.HasKey(b => new { b.AccountId, b.MarketId });
            bot.Property(b => b.MarketId).HasMaxLength(32);
            bot.HasOne<Account>().WithMany().HasForeignKey(b => b.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<MarketInterest>(interest =>
        {
            interest.HasKey(i => i.AccountId);
            interest.HasOne<Account>().WithMany().HasForeignKey(i => i.AccountId).OnDelete(DeleteBehavior.Cascade);
            interest.HasOne<GeoCity>().WithMany().HasForeignKey(i => i.GeoNameId).OnDelete(DeleteBehavior.Restrict);
            interest.HasIndex(i => i.GeoNameId);
        });
        model.Entity<Itinerary>(itinerary =>
        {
            itinerary.HasOne<Account>().WithMany().HasForeignKey(i => i.AccountId).OnDelete(DeleteBehavior.Cascade);
            itinerary.HasIndex(i => i.AccountId);
        });
        model.Entity<Trip>(trip =>
        {
            trip.HasOne<Account>().WithMany().HasForeignKey(t => t.AccountId).OnDelete(DeleteBehavior.Cascade);
            trip.HasOne<GeoCity>().WithMany().HasForeignKey(t => t.GeoNameId).OnDelete(DeleteBehavior.Restrict);
            trip.HasOne<Itinerary>().WithMany().HasForeignKey(t => t.ItineraryId).OnDelete(DeleteBehavior.SetNull);
            trip.HasIndex(t => new { t.AccountId, t.Arrival });
            trip.HasIndex(t => new { t.GeoNameId, t.Departure });
            trip.ToTable(t => t.HasCheckConstraint("ck_trips_dates", "departure >= arrival"));
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
