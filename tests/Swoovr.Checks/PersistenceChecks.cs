using Microsoft.EntityFrameworkCore;
using Npgsql;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

/// <summary>
/// Runs against a real PostgreSQL when SWOOVR_TEST_DATABASE holds an admin connection string
/// (scripts/check.sh sets it for the local container). Each run creates and drops its own
/// throwaway database, so it never touches the application's data.
/// </summary>
static class PersistenceChecks
{
    public static async Task Run()
    {
        var admin = Environment.GetEnvironmentVariable("SWOOVR_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(admin))
        {
            Console.WriteLine("SKIP: persistence checks (SWOOVR_TEST_DATABASE not set; no local PostgreSQL).");
            return;
        }
        var name = "swoovr_check_" + Guid.NewGuid().ToString("N")[..12];
        await Execute(admin, $"CREATE DATABASE {name}");
        var count = 0;
        void Check(string what, bool condition)
        {
            if (!condition) throw new Exception($"Persistence: {what}");
            count++;
        }
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(admin) { Database = name, Pooling = false }.ConnectionString;
            DbContextOptions<SwoovrDbContext> options = new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(connection).Options;
            await using (var db = new SwoovrDbContext(options))
            {
                await db.Database.MigrateAsync("20261029011346_BanCheckQuotas");
                var legacyEmail = Guid.NewGuid();var legacyEmpty = Guid.NewGuid();
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO accounts(id,issuer,subject,realm,email,status,is_root_admin,created_at,last_seen_at)
                    VALUES({legacyEmail},'https://issuer.example.test','fictional-clock-email','Members','fictional.clock@example.test','Active',false,now()-interval '1 day',now()),
                          ({legacyEmpty},'https://issuer.example.test','fictional-clock-empty','Members',NULL,'Active',false,now()-interval '1 day',now())
                    """);
                var beforeUpgrade = DateTimeOffset.UtcNow;
                await db.Database.MigrateAsync();
                var acceptedAt = await db.Accounts.Where(a=>a.Id==legacyEmail).Select(a=>a.EmailAcceptedAt).SingleAsync();
                Check("legacy accepted email gets conservative migration watermark",acceptedAt>=beforeUpgrade && acceptedAt<=DateTimeOffset.UtcNow.AddSeconds(5));
                Check("legacy null email gets no invented acceptance clock",await db.Accounts.Where(a=>a.Id==legacyEmpty).Select(a=>a.EmailAcceptedAt).SingleAsync() is null);
                await db.Database.MigrateAsync();
                Check("repeat migration cannot advance accepted email clock",await db.Accounts.Where(a=>a.Id==legacyEmail).Select(a=>a.EmailAcceptedAt).SingleAsync()==acceptedAt);
                await db.Accounts.Where(a=>a.Id==legacyEmail||a.Id==legacyEmpty).ExecuteDeleteAsync();
                await RegistryDispositionPersistenceChecks.Run(options, Check);
                Check("no pending migrations", !(await db.Database.GetPendingMigrationsAsync()).Any());
            }

            var now = DateTimeOffset.Parse("2026-10-23T00:00:00Z");
            var id = Guid.NewGuid();
            await using (var db = new SwoovrDbContext(options))
            {
                db.Accounts.Add(new Account { Id = id, Issuer = "https://issuer.example.test/realms/members", Subject = "s-1", Realm = AccountRealm.Members, Status = AccountStatus.Active, CreatedAt = now, LastSeenAt = now });
                db.MemberProfiles.Add(new MemberProfile { AccountId = id, AgeStatus = AgeStatus.Attested, DateOfBirth = new DateOnly(1990, 5, 1) });
                await db.SaveChangesAsync();
            }
            await using (var db = new SwoovrDbContext(options))
            {
                db.Accounts.Add(new Account { Id = Guid.NewGuid(), Issuer = "https://issuer.example.test/realms/members", Subject = "s-1", Realm = AccountRealm.Members, CreatedAt = now, LastSeenAt = now });
                Check("(issuer, subject) is unique", await Throws(db.SaveChangesAsync()));
            }
            await using (var db = new SwoovrDbContext(options))
            {
                db.Accounts.Add(new Account { Id = Guid.NewGuid(), Issuer = "https://issuer.example.test/realms/staff", Subject = "s-1", Realm = AccountRealm.Staff, CreatedAt = now, LastSeenAt = now });
                await db.SaveChangesAsync();
                Check("same subject under another issuer is another account", await db.Accounts.CountAsync() == 2);
                var stored = await db.Database.SqlQueryRaw<string>("SELECT age_status AS \"Value\" FROM member_profiles").SingleAsync();
                Check("enums stored by name", stored == "Attested");
                var profile = await db.MemberProfiles.SingleAsync();
                Check("date of birth round-trips", profile.DateOfBirth == new DateOnly(1990, 5, 1));
                db.RoleGrants.Add(new RoleGrant { Id = Guid.NewGuid(), AccountId = id, Role = AccountRole.Admin, GrantedAt = now });
                await db.SaveChangesAsync();
            }
            await using (var db = new SwoovrDbContext(options))
            {
                db.RoleGrants.Add(new RoleGrant { Id = Guid.NewGuid(), AccountId = id, Role = AccountRole.Admin, GrantedAt = now });
                Check("a role is granted once even without a venue", await Throws(db.SaveChangesAsync()));
            }
            await using (var db = new SwoovrDbContext(options))
            {
                db.RoleGrants.Add(new RoleGrant { Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), Role = AccountRole.Admin, GrantedAt = now });
                Check("a grant needs an account", await Throws(db.SaveChangesAsync()));
            }
            await using (var db = new SwoovrDbContext(options))
            {
                db.Accounts.Remove(await db.Accounts.SingleAsync(a => a.Id == id));
                await db.SaveChangesAsync();
                Check("profile and grants go with the account", !await db.MemberProfiles.AnyAsync() && !await db.RoleGrants.AnyAsync());
            }
            await Extra(options, Check);
            await Modes(options, Check);
            await Travel(options, Check);
            await Messaging(options, Check);
            await FallbackKeyPersistenceChecks.Run(options, Check);
            await StaffConflictPersistenceChecks.Run(options, Check);
            await BannedIdentifierPersistenceChecks.Run(options, Check);
            await EmailRetirementPersistenceChecks.Run(options, Check);
            await OperationalRetentionChecks.Run(options, Check);
            await MeetingSafetyPersistenceChecks.Run(options, Check);
            await ContentRemovalPersistenceChecks.Run(options, Check);
            await TextHarmPersistenceChecks.Run(options, Check);
            await TextHarmRuntimeChecks.Run(options, Check);
            await ContentRemovalGuardChecks.Run(options, Check);
            await MeetingSafetyContactVerificationPersistenceChecks.Run(options, Check);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await Execute(admin, $"DROP DATABASE IF EXISTS {name} WITH (FORCE)");
        }
        Console.WriteLine($"PASS: {count} persistence cases against PostgreSQL (migrations, keys, enums, cascades).");
    }

    /// <summary>D143: one open conversation per ordered pair; closed ones stay for evidence; messages and hides cascade.</summary>
    static async Task Messaging(DbContextOptions<SwoovrDbContext> options, Action<string, bool> check)
    {
        var now = DateTimeOffset.Parse("2026-10-26T00:00:00Z");
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var (low, high) = ConnectionRequest.Pair(a, b);
        await using var db = new SwoovrDbContext(options);
        foreach (var id in new[] { a, b })
            db.Accounts.Add(new Account { Id = id, Issuer = "https://issuer.example.test/realms/members", Subject = "chat-" + id, Realm = AccountRealm.Members, CreatedAt = now, LastSeenAt = now });
        var first = new Conversation { Id = Guid.NewGuid(), PairLow = low, PairHigh = high, CreatedAt = now };
        db.Conversations.Add(first);
        var message = new Message { Id = Guid.NewGuid(), ConversationId = first.Id, SenderAccountId = a, Kind = MessageKind.Text, Body = "Fictional hello", SentAt = now };
        db.Messages.Add(message);
        db.MessageHides.Add(new MessageHide { MessageId = message.Id, AccountId = b, HiddenAt = now });
        db.ConversationHides.Add(new ConversationHide { ConversationId = first.Id, AccountId = b, HiddenAt = now });
        await db.SaveChangesAsync();
        await using (var bad = new SwoovrDbContext(options))
        {
            bad.Conversations.Add(new Conversation { Id = Guid.NewGuid(), PairLow = low, PairHigh = high, CreatedAt = now });
            check("one open conversation per pair", await Throws(bad.SaveChangesAsync()));
        }
        await using (var bad = new SwoovrDbContext(options))
        {
            bad.Conversations.Add(new Conversation { Id = Guid.NewGuid(), PairLow = high, PairHigh = low, CreatedAt = now, ClosedAt = now });
            check("the pair is stored in order", await Throws(bad.SaveChangesAsync()));
        }
        await using (var bad = new SwoovrDbContext(options))
        {
            bad.Messages.Add(new Message { Id = Guid.NewGuid(), ConversationId = first.Id, SenderAccountId = a, Body = new string('x', MessagingPolicy.TextMax + 1), SentAt = now });
            check("a message is at most 2,000 characters", await Throws(bad.SaveChangesAsync()));
        }
        first.ClosedAt = now;
        db.Conversations.Add(new Conversation { Id = Guid.NewGuid(), PairLow = low, PairHigh = high, CreatedAt = now });
        await db.SaveChangesAsync();
        check("a closed conversation stays and a new one can open", await db.Conversations.CountAsync(c => c.PairLow == low) == 2 && await db.Messages.AnyAsync(m => m.ConversationId == first.Id));
        db.Accounts.Remove(await db.Accounts.SingleAsync(x => x.Id == a));
        await db.SaveChangesAsync();
        check("conversations, messages and hides go with the account", !await db.Conversations.AnyAsync(c => c.PairLow == low) && !await db.Messages.AnyAsync(m => m.Id == message.Id)
            && !await db.MessageHides.AnyAsync(h => h.MessageId == message.Id) && !await db.ConversationHides.AnyAsync(h => h.ConversationId == first.Id));
    }

    /// <summary>Further persistence cases added by later slices.</summary>
    /// <summary>D92 tables: seeded modes, stable keys, cascades and the public id default.</summary>
    static async Task Modes(DbContextOptions<SwoovrDbContext> options, Action<string, bool> check)
    {
        var now = DateTimeOffset.Parse("2026-10-23T00:00:00Z");
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        await using (var db = new SwoovrDbContext(options))
        {
            var seeded = await db.ConnectionModes.OrderBy(m => m.SortOrder).Select(m => m.Id + ":" + m.Label).ToListAsync();
            check("four modes seeded in order", seeded.SequenceEqual(["dating:Dating", "friends:Friends", "activities:Activities", "business:Business"]));
            var chips = await db.ModeIntentions.OrderBy(i => i.ModeId).ThenBy(i => i.SortOrder).ToListAsync();
            check("forty approved chips seeded with their fixed ids (Q16, D113)", chips.Count == 40
                && chips.Select(i => (i.Id, i.Label)).ToHashSet().SetEquals(IntentionChips.All.Select(i => (i.Id, i.Label))));
            foreach (var id in new[] { a, b })
            {
                db.Accounts.Add(new Account { Id = id, Issuer = "https://issuer.example.test/realms/members", Subject = "modes-" + id, Realm = AccountRealm.Members, CreatedAt = now, LastSeenAt = now });
                db.MemberProfiles.Add(new MemberProfile { AccountId = id, AgeStatus = AgeStatus.Attested });
            }
            await db.SaveChangesAsync();
            var ids = await db.MemberProfiles.Where(p => p.AccountId == a || p.AccountId == b).Select(p => p.PublicId).ToListAsync();
            check("public ids generated and distinct", ids.All(i => i != Guid.Empty) && ids.Distinct().Count() == 2);
        }
        var chip = IntentionChips.Id(1, 1);
        await using (var db = new SwoovrDbContext(options))
        {
            db.MemberModes.Add(new MemberMode { AccountId = a, ModeId = ConnectionModeIds.Dating, Enabled = true, Visible = true, UpdatedAt = now });
            db.MemberModes.Add(new MemberMode { AccountId = a, ModeId = ConnectionModeIds.Friends, Enabled = true, Visible = false, UpdatedAt = now });
            db.MemberModeIntentions.Add(new MemberModeIntention { AccountId = a, ModeId = ConnectionModeIds.Dating, IntentionId = chip });
            await db.SaveChangesAsync();
            check("several modes at once", await db.MemberModes.CountAsync(m => m.AccountId == a) == 2);
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.MemberModes.Add(new MemberMode { AccountId = a, ModeId = ConnectionModeIds.Dating, UpdatedAt = now });
            check("one setting per member and mode", await Throws(db.SaveChangesAsync()));
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.MemberModes.Add(new MemberMode { AccountId = b, ModeId = "travel", UpdatedAt = now });
            check("no setting for a mode that does not exist (travel is not a mode)", await Throws(db.SaveChangesAsync()));
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.MemberModeIntentions.Add(new MemberModeIntention { AccountId = b, ModeId = ConnectionModeIds.Dating, IntentionId = chip });
            check("an intention needs the member's setting for that mode", await Throws(db.SaveChangesAsync()));
        }
        await using (var db = new SwoovrDbContext(options))
        {
            var order = await db.BadgeDefinitions.OrderBy(d => d.SortOrder).Select(d => d.Badge).ToListAsync();
            check("badge order seeded as data (Q11 rule 61)", order.SequenceEqual(BadgePolicy.DefaultOrder));
            db.BotModeAuthorisations.Add(new BotModeAuthorisation { AccountId = a, ModeId = ConnectionModeIds.Dating, AuthorisedAt = now });
            await db.SaveChangesAsync();
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.BotModeAuthorisations.Add(new BotModeAuthorisation { AccountId = a, ModeId = ConnectionModeIds.Dating, AuthorisedAt = now });
            check("one authorisation per bot and mode", await Throws(db.SaveChangesAsync()));
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.BotModeAuthorisations.Add(new BotModeAuthorisation { AccountId = b, ModeId = "travel", AuthorisedAt = now });
            check("a bot can only be authorised for a real mode", await Throws(db.SaveChangesAsync()));
        }
        // D112: one pending and one accepted request per pair and mode, whichever way; no self requests or blocks.
        ConnectionRequest Request(Guid from, Guid to, ConnectionRequestStatus status, string mode = ConnectionModeIds.Dating)
        {
            var (low, high) = ConnectionRequest.Pair(from, to);
            return new() { Id = Guid.NewGuid(), FromAccountId = from, ToAccountId = to, PairLow = low, PairHigh = high, ModeId = mode, Status = status, CreatedAt = now };
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.ConnectionRequests.Add(Request(a, b, ConnectionRequestStatus.Pending));
            db.ConnectionRequests.Add(Request(b, a, ConnectionRequestStatus.Pending, ConnectionModeIds.Friends));
            db.ConnectionRequests.Add(Request(a, b, ConnectionRequestStatus.Declined));
            await db.SaveChangesAsync();
            check("a pending request per mode, and settled ones beside it", await db.ConnectionRequests.CountAsync() == 3);
        }
        foreach (var (what, row) in new (string, ConnectionRequest)[] { ("a second pending request the other way", Request(b, a, ConnectionRequestStatus.Pending)),
            ("a request to oneself", Request(a, a, ConnectionRequestStatus.Pending, ConnectionModeIds.Business)) })
            await using (var db = new SwoovrDbContext(options))
            {
                db.ConnectionRequests.Add(row);
                check(what + " is refused by the database", await Throws(db.SaveChangesAsync()));
            }
        await using (var db = new SwoovrDbContext(options))
        {
            db.ConnectionRequests.Add(Request(a, b, ConnectionRequestStatus.Accepted, ConnectionModeIds.Activities));
            await db.SaveChangesAsync();
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.ConnectionRequests.Add(Request(b, a, ConnectionRequestStatus.Accepted, ConnectionModeIds.Activities));
            check("one connection per pair and mode", await Throws(db.SaveChangesAsync()));
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.Blocks.Add(new Block { BlockerAccountId = a, BlockedAccountId = b, CreatedAt = now });
            db.Blocks.Add(new Block { BlockerAccountId = b, BlockedAccountId = a, CreatedAt = now });
            await db.SaveChangesAsync();
            db.Blocks.Add(new Block { BlockerAccountId = a, BlockedAccountId = a, CreatedAt = now });
            check("no blocking oneself", await Throws(db.SaveChangesAsync()));
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.Accounts.Remove(await db.Accounts.SingleAsync(x => x.Id == a));
            await db.SaveChangesAsync();
            check("mode settings, intentions and bot authorisations go with the account", !await db.MemberModes.AnyAsync(m => m.AccountId == a)
                && !await db.MemberModeIntentions.AnyAsync() && !await db.BotModeAuthorisations.AnyAsync());
            check("requests and blocks go with either account", !await db.ConnectionRequests.AnyAsync() && !await db.Blocks.AnyAsync());
        }
    }

    /// <summary>D110–D111: the GeoNames sample import (twice, as an upsert), search keys, the seeded market, trip constraints.</summary>
    static async Task Travel(DbContextOptions<SwoovrDbContext> options, Action<string, bool> check)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "geonames");
        await using (var db = new SwoovrDbContext(options))
        {
            var first = await Swoovr.Infrastructure.Places.GeoNamesImport.ImportAsync(db, folder);
            var again = await Swoovr.Infrastructure.Places.GeoNamesImport.ImportAsync(db, folder);
            check("sample imported, and importing again changes nothing", first == again && await db.GeoCities.CountAsync() == first.Cities && first.Cities == 21
                && await db.GeoCityNames.CountAsync() == first.Names);
            var adelaide = await db.GeoCities.SingleAsync(c => c.GeoNameId == 2078025);
            check("city with region, country and time zone", adelaide is { Name: "Adelaide", Admin1Name: "South Australia", CountryName: "Australia", TimeZone: "Australia/Adelaide" });
            check("alternate names are searchable, folded", await db.GeoCityNames.AnyAsync(n => n.GeoNameId == 2988507 && n.Key == "parigi")
                && await db.GeoCityNames.CountAsync(n => n.Key == "norwood") == 4);
            var market = await db.Markets.SingleAsync();
            check("Adelaide is the only market, a beta market (D110, D125)", market is { Id: "adelaide", Status: MarketStatus.Beta, Open: true });
            check("one 50 km area around Adelaide and all four modes (D124, D146)", await db.MarketAreas.SingleAsync() is { MarketId: "adelaide", CentreGeoNameId: 2078025, RadiusKm: 50 }
                && (await db.MarketModes.Where(m => m.MarketId == "adelaide").Select(m => m.ModeId).OrderBy(m => m).ToListAsync()).SequenceEqual(["activities", "business", "dating", "friends"]));
            await using (var bad = new SwoovrDbContext(options))
            {
                bad.MarketAreas.Add(new MarketArea { MarketId = "adelaide", CentreGeoNameId = 2158177, RadiusKm = 0 });
                check("a market area needs a positive radius", await Throws(bad.SaveChangesAsync()));
            }
            var id = Guid.NewGuid();
            var now = DateTimeOffset.Parse("2026-10-24T00:00:00Z");
            db.Accounts.Add(new Account { Id = id, Issuer = "https://issuer.example.test/realms/members", Subject = "travel-" + id, Realm = AccountRealm.Members, CreatedAt = now, LastSeenAt = now });
            db.MemberProfiles.Add(new MemberProfile { AccountId = id, AgeStatus = AgeStatus.Attested, HomeGeoNameId = 2078025 });
            db.Trips.Add(new Trip { Id = Guid.NewGuid(), AccountId = id, GeoNameId = 2158177, Arrival = new DateOnly(2026, 11, 1), Departure = new DateOnly(2026, 11, 3), CreatedAt = now, UpdatedAt = now });
            await db.SaveChangesAsync();
            check("a trip and a home city reference GeoNames ids", await db.Trips.CountAsync(t => t.AccountId == id) == 1);
            // D149, D150: arrival keeps the time, market and method; home checks keep the outcome. No coordinate columns exist.
            var arrived = await db.Trips.SingleAsync(t => t.AccountId == id);
            (arrived.ArrivedAt, arrived.ArrivalMarketId, arrived.ArrivalMethod, arrived.ArrivalOutcome) = (now, "adelaide", ArrivalMethod.DeviceLocation, ArrivalOutcome.Confirmed);
            db.HomeLocationChecks.Add(new HomeLocationCheck { Id = Guid.NewGuid(), AccountId = id, CheckedAt = now, Method = LocationMethod.Device, HomeMarketId = "adelaide", Outcome = HomeConsistency.Inconsistent });
            await db.SaveChangesAsync();
            await using (var fresh = new SwoovrDbContext(options))
                check("an arrival and a home check read back as stored", await fresh.Trips.SingleAsync(t => t.AccountId == id) is { ArrivalMarketId: "adelaide", ArrivalMethod: ArrivalMethod.DeviceLocation, ArrivalOutcome: ArrivalOutcome.Confirmed }
                    && await fresh.HomeLocationChecks.SingleAsync(c => c.AccountId == id) is { Method: LocationMethod.Device, Outcome: HomeConsistency.Inconsistent });
            var locationColumns = db.Model.GetEntityTypes().Where(e => e.ClrType == typeof(Trip) || e.ClrType == typeof(HomeLocationCheck))
                .SelectMany(e => e.GetProperties()).Select(p => p.Name.ToLowerInvariant()).ToList();
            check("no coordinates, IP addresses or places are stored for arrival or home checks", !locationColumns.Any(c => c.Contains("latitude") || c.Contains("longitude") || c.Contains("ip") || c.Contains("city") || c.Contains("accuracy")));
            await using (var bad = new SwoovrDbContext(options))
            {
                bad.Trips.Add(new Trip { Id = Guid.NewGuid(), AccountId = id, GeoNameId = 2158177, Arrival = new DateOnly(2026, 11, 1), Departure = new DateOnly(2026, 11, 3), CreatedAt = now, UpdatedAt = now, ArrivalMarketId = "nowhere" });
                check("an arrival names a real market", await Throws(bad.SaveChangesAsync()));
            }
            foreach (var (what, trip) in new (string, Trip)[] {
                ("a trip to a city that isn't in geo_cities", new Trip { Id = Guid.NewGuid(), AccountId = id, GeoNameId = 1, Arrival = new DateOnly(2026, 11, 1), Departure = new DateOnly(2026, 11, 2), CreatedAt = now, UpdatedAt = now }),
                ("departure before arrival", new Trip { Id = Guid.NewGuid(), AccountId = id, GeoNameId = 2158177, Arrival = new DateOnly(2026, 11, 5), Departure = new DateOnly(2026, 11, 2), CreatedAt = now, UpdatedAt = now }) })
                await using (var bad = new SwoovrDbContext(options))
                {
                    bad.Trips.Add(trip);
                    check(what + " is refused by the database", await Throws(bad.SaveChangesAsync()));
                }
            db.Accounts.Remove(await db.Accounts.SingleAsync(a => a.Id == id));
            await db.SaveChangesAsync();
            check("trips and home checks go with the account", !await db.Trips.AnyAsync(t => t.AccountId == id) && !await db.HomeLocationChecks.AnyAsync(c => c.AccountId == id));
            var bot = Guid.NewGuid();
            db.Accounts.Add(new Account { Id = bot, Issuer = "https://issuer.example.test/realms/members", Subject = "bot-" + bot, Realm = AccountRealm.Members, CreatedAt = now, LastSeenAt = now });
            db.MemberProfiles.Add(new MemberProfile { AccountId = bot, AgeStatus = AgeStatus.Attested, Bot = true, HomeGeoNameId = 2078025, HomeMarketId = "adelaide" });
            db.BotMarkets.Add(new BotMarket { AccountId = bot, MarketId = BotMarket.Everywhere, AuthorisedAt = now });
            db.MarketInterests.Add(new MarketInterest { AccountId = bot, GeoNameId = 2158177, CreatedAt = now });
            await db.SaveChangesAsync();
            db.Markets.Remove(await db.Markets.SingleAsync());
            await db.SaveChangesAsync();
            check("removing a market leaves its members outside every market and takes its areas and modes (D146)",
                (await db.MemberProfiles.SingleAsync(p => p.AccountId == bot)).HomeMarketId is null && !await db.MarketAreas.AnyAsync() && !await db.MarketModes.AnyAsync());
            db.Accounts.Remove(await db.Accounts.SingleAsync(a => a.Id == bot));
            await db.SaveChangesAsync();
            check("bot markets and launch interest go with the account", !await db.BotMarkets.AnyAsync() && !await db.MarketInterests.AnyAsync());
        }
    }

    static async Task Extra(DbContextOptions<SwoovrDbContext> options, Action<string, bool> check)
    {
        var now = DateTimeOffset.Parse("2026-10-23T00:00:00Z");
        await using (var db = new SwoovrDbContext(options))
        {
            db.BetaEnrolments.Add(new BetaEnrolment { Id = Guid.NewGuid(), Email = "beta@example.test", CreatedAt = now, RevokedAt = now });
            db.BetaEnrolments.Add(new BetaEnrolment { Id = Guid.NewGuid(), Email = "beta@example.test", CreatedAt = now });
            await db.SaveChangesAsync();
            check("a revoked enrolment can be added again", await db.BetaEnrolments.CountAsync() == 2);
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.BetaEnrolments.Add(new BetaEnrolment { Id = Guid.NewGuid(), Email = "beta@example.test", CreatedAt = now });
            check("one live enrolment per address", await Throws(db.SaveChangesAsync()));
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.FounderPlaces.Add(new FounderPlace { Place = 0, GrantedAt = now });
            check("founder places start at 1", await Throws(db.SaveChangesAsync()));
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.FounderPlaces.Add(new FounderPlace { Place = 1, GrantedAt = now });
            db.FounderTokens.Add(new FounderToken { Id = Guid.NewGuid(), Place = 1, Code = "FOUNDER-V1-A", SecretVersion = 1, IssuedAt = now });
            await db.SaveChangesAsync();
            db.FounderTokens.Add(new FounderToken { Id = Guid.NewGuid(), Place = 1, Code = "FOUNDER-V1-A", SecretVersion = 1, IssuedAt = now });
            check("a founder code is stored once", await Throws(db.SaveChangesAsync()));
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.FounderTokens.Add(new FounderToken { Id = Guid.NewGuid(), Place = 99, Code = "FOUNDER-V1-B", SecretVersion = 1, IssuedAt = now });
            check("a token needs its place", await Throws(db.SaveChangesAsync()));
        }
        await using (var db = new SwoovrDbContext(options))
        {
            var first = Guid.NewGuid(); var second = Guid.NewGuid();
            foreach (var id in new[] { first, second })
                db.Accounts.Add(new Account { Id = id, Issuer = "https://issuer.example.test/realms/members", Subject = id.ToString(), Realm = AccountRealm.Members, CreatedAt = now, LastSeenAt = now });
            db.MemberProfiles.Add(new MemberProfile { AccountId = first, FoundingMember = true, FounderPlace = 1 });
            db.MemberProfiles.Add(new MemberProfile { AccountId = second });
            await db.SaveChangesAsync();
            (await db.MemberProfiles.SingleAsync(p => p.AccountId == second)).FounderPlace = 1;
            check("one live account per founding place", await Throws(db.SaveChangesAsync()));
        }
    }

    static async Task<bool> Throws(Task task)
    {
        try { await task; return false; }
        catch (DbUpdateException) { return true; }
    }

    static async Task Execute(string connection, string sql)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand(sql, db);
        await command.ExecuteNonQueryAsync();
    }
}
