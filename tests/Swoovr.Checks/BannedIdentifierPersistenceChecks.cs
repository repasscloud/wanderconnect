using Microsoft.EntityFrameworkCore;
using Npgsql;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

/// <summary>Disposable PostgreSQL only: registry constraints, immutable history and atomic transition/note rollback.</summary>
static class BannedIdentifierPersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options, Action<string, bool> check)
    {
        await using var db = new SwoovrDbContext(options);
        var now = new DateTimeOffset(DateTimeOffset.UtcNow.UtcTicks / 10 * 10, TimeSpan.Zero);var member = Guid.NewGuid();var staff = Guid.NewGuid();var review = Guid.NewGuid();
        db.Accounts.Add(new Account { Id = member, Issuer = "https://issuer.example.test/realms/members", Subject = "fictional-ban-" + member, Realm = AccountRealm.Members, Status = AccountStatus.Deleted, CreatedAt = now, LastSeenAt = now });
        db.ModerationCases.Add(new ModerationCase { Id = review, SubjectAccountId = member, Status = CaseStatus.Closed, Queue = CaseQueue.Admin, CreatedAt = now, UpdatedAt = now, ClosedAt = now });
        await db.SaveChangesAsync();
        var key = Enumerable.Repeat((byte)31,32).ToArray();
        var email = "fictional.registry@example.test";
        BannedIdentifier NewRow() => new() { Id = Guid.NewGuid(), IdentifierType = BannedIdentifierType.Email,
            IdentifierHash = BannedIdentifierPolicy.Fingerprint(BannedIdentifierType.Email,email,key), KeyVersion = 1,
            CaseId = review, BanReasonCode = "permanent-account-ban", Status = BanStatus.Active, BannedAt = now };
        var row = NewRow();db.BannedIdentifiers.Add(row);await db.SaveChangesAsync();
        var hash = BannedIdentifierPolicy.Fingerprint(BannedIdentifierType.Email,email,key);
        check("real registry matches active indefinite typed/versioned hash", await db.BannedIdentifiers.AnyAsync(b => b.IdentifierType == BannedIdentifierType.Email && b.KeyVersion == 1 && b.IdentifierHash == hash && b.Status == BanStatus.Active && (b.BanExpiresAt == null || b.BanExpiresAt > now)));
        row.BanExpiresAt = now.AddSeconds(1);await db.SaveChangesAsync();
        check("future expiry blocks in SQL without a worker", await db.BannedIdentifiers.AnyAsync(b => b.Id == row.Id && b.Status == BanStatus.Active && (b.BanExpiresAt == null || b.BanExpiresAt > now)));
        row.BanExpiresAt = now;await db.SaveChangesAsync();
        check("exact expiry allows in SQL while status is still Active", !await db.BannedIdentifiers.AnyAsync(b => b.Id == row.Id && b.Status == BanStatus.Active && (b.BanExpiresAt == null || b.BanExpiresAt > now)));
        row.BanExpiresAt = now.AddSeconds(-1);await db.SaveChangesAsync();
        check("past expiry allows in SQL without worker state transition", !await db.BannedIdentifiers.AnyAsync(b => b.Id == row.Id && b.Status == BanStatus.Active && (b.BanExpiresAt == null || b.BanExpiresAt > now)));
        row.BanExpiresAt = null;
        var sanction = new Sanction { Id = Guid.NewGuid(), AccountId = member, CaseId = review, Kind = SanctionKind.Termination, Reason = "Fictional permanent enforcement", StartsAt = now, CreatedBy = staff, CreatedByRole = AccountRole.Admin };
        db.Sanctions.Add(sanction);await db.SaveChangesAsync();
        check("deleted member status cannot cascade-remove the registry", await db.BannedIdentifiers.AnyAsync(b => b.Id == row.Id));
        check("registry cannot match another identifier type", !await db.BannedIdentifiers.AnyAsync(b => b.IdentifierType == BannedIdentifierType.Mobile && b.IdentifierHash == hash));
        foreach (var change in new Action<BannedIdentifier>[] {
            b => b.IdentifierHash = new byte[31], b => b.KeyVersion = 0, b => b.IdentifierType = (BannedIdentifierType)99,
            b => b.Status = (BanStatus)99, b => b.BanReasonCode = "", b => b.MatchCount = -1,
            b => b.Status = BanStatus.Revoked, b => b.Status = BanStatus.Expired,
            b => b.CaseId = Guid.NewGuid(),
        }) {
            await using var invalid = new SwoovrDbContext(options);var candidate = NewRow();change(candidate);invalid.BannedIdentifiers.Add(candidate);
            var refused = false;try { await invalid.SaveChangesAsync(); } catch (DbUpdateException) { refused = true; }
            check("database rejects malformed registry provenance/state/hash", refused);
        }
        // Force the associated case note insert to fail after constructing the complete intended revocation.
        await using (var failing = new SwoovrDbContext(options)) {
            var current = await failing.BannedIdentifiers.SingleAsync(b => b.Id == row.Id);
            var (transition,note) = BannedIdentifierPolicy.Revoke(current,staff,"Fictional reassigned mobile ownership reviewed by staff.",now);
            note.Text = new string('x',1001);failing.BanTransitions.Add(transition);failing.BanCaseNotes.Add(note);
            var failed = false;try { await failing.SaveChangesAsync(); } catch (DbUpdateException) { failed = true; }
            check("failed case note aborts the complete intended revocation", failed);
        }
        await db.Entry(row).ReloadAsync();
        check("failed note leaves restriction active and creates no audit or note", row.Status == BanStatus.Active && !await db.BanTransitions.AnyAsync(t => t.BannedIdentifierId == row.Id) && !await db.BanCaseNotes.AnyAsync(n => n.CaseId == review));
        var (committed,caseNote) = BannedIdentifierPolicy.Revoke(row,staff,"Fictional reassigned identifier reviewed with sufficient ownership evidence.",now);
        db.BanTransitions.Add(committed);db.BanCaseNotes.Add(caseNote);await db.SaveChangesAsync();
        check("registry revocation and linked transition/note commit together", await db.BannedIdentifiers.AnyAsync(b => b.Id == row.Id && b.Status == BanStatus.Revoked && b.RevokedBy == staff && b.RevokedAt == now && b.BanExpiresAt == now && b.LastReviewedAt == now)
            && await db.BanTransitions.AnyAsync(t => t.Id == committed.Id && t.OldStatus == BanStatus.Active && t.NewStatus == BanStatus.Revoked)
            && await db.BanCaseNotes.AnyAsync(n => n.TransitionId == committed.Id && n.CaseId == review && n.AuthorAccountId == staff));
        check("identifier release leaves original account deletion and termination unchanged", await db.Accounts.AnyAsync(a => a.Id == member && a.Status == AccountStatus.Deleted)
            && await db.Sanctions.AnyAsync(s => s.Id == sanction.Id && s.Kind == SanctionKind.Termination && s.LiftedAt == null));
        check("restricted transition snapshots never contain plaintext fixture email", !committed.OldValues.Contains(email) && !committed.NewValues.Contains(email));
        check("revoked identifier is not an active database match", !await db.BannedIdentifiers.AnyAsync(b => b.IdentifierHash == hash && b.Status == BanStatus.Active && (b.BanExpiresAt == null || b.BanExpiresAt > now)));
        foreach (var table in new[] { "ban_transitions", "ban_case_notes" }) {
            foreach (var sql in new[] { $"UPDATE {table} SET at = at", $"DELETE FROM {table}", $"TRUNCATE {table} CASCADE" }) {
                var refused = false;try { await db.Database.ExecuteSqlRawAsync(sql); } catch (PostgresException e) when (e.MessageText.Contains("append-only")) { refused = true; }
                check("restricted history refuses ordinary update/delete/truncate", refused);
            }
        }
        check("failed tampering leaves both history records intact", await db.BanTransitions.CountAsync(t => t.BannedIdentifierId == row.Id) == 1 && await db.BanCaseNotes.CountAsync(n => n.TransitionId == committed.Id) == 1);
        var expiryRow=NewRow();expiryRow.BanExpiresAt=now;db.BannedIdentifiers.Add(expiryRow);
        var future=NewRow();future.BanExpiresAt=now.AddSeconds(1);db.BannedIdentifiers.Add(future);
        var indefinite=NewRow();db.BannedIdentifiers.Add(indefinite);await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION expiry_fixture_refuse() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.text LIKE 'Scheduled identifier restriction expired.%' THEN RAISE EXCEPTION 'fictional expiry note fault'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER expiry_fixture_refuse BEFORE INSERT ON ban_case_notes FOR EACH ROW EXECUTE FUNCTION expiry_fixture_refuse();
            """);
        await using(var failing=new SwoovrDbContext(options))
        {
            var refused=false;try{await new Swoovr.Api.Accounts.RegistryExpiry(failing).RunAsync(now,CancellationToken.None);}catch(DbUpdateException){refused=true;}
            check("expiry note fault rolls back intended status transition",refused);
        }
        await db.Entry(expiryRow).ReloadAsync();
        check("failed expiry preserves active status and has no orphan event",expiryRow.Status==BanStatus.Active&&!await db.BanTransitions.AnyAsync(t=>t.BannedIdentifierId==expiryRow.Id));
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER expiry_fixture_refuse ON ban_case_notes; DROP FUNCTION expiry_fixture_refuse();");
        async Task<int> Sweep(){await using var replica=new SwoovrDbContext(options);return await new Swoovr.Api.Accounts.RegistryExpiry(replica).RunAsync(now,CancellationToken.None);}
        var replicas=await Task.WhenAll(Sweep(),Sweep());
        check("two expiry replicas commit one transition at exact boundary",replicas.Sum()==1&&await db.BanTransitions.CountAsync(t=>t.BannedIdentifierId==expiryRow.Id&&t.Action=="expired")==1);
        await db.Entry(expiryRow).ReloadAsync();await db.Entry(future).ReloadAsync();await db.Entry(indefinite).ReloadAsync();
        check("expiry preserves original clock and distinguishes future/indefinite/revoked",expiryRow.Status==BanStatus.Expired&&expiryRow.BanExpiresAt==now&&expiryRow.LastReviewedAt==null&&future.Status==BanStatus.Active&&indefinite.Status==BanStatus.Active&&row.Status==BanStatus.Revoked);
        check("expiry note and system event retain associated source",await db.BanCaseNotes.AnyAsync(n=>n.CaseId==review&&n.AuthorAccountId==null&&db.BanTransitions.Any(t=>t.Id==n.TransitionId&&t.BannedIdentifierId==expiryRow.Id&&t.ActorAccountId==null&&t.NewStatus==BanStatus.Expired)));
        check("expiry rerun is idempotent and leaves original termination",await Sweep()==0&&await db.Sanctions.AnyAsync(x=>x.Id==sanction.Id&&x.LiftedAt==null));
        for(var i=0;i<201;i++){var bulk=NewRow();bulk.BanExpiresAt=now;db.BannedIdentifiers.Add(bulk);}await db.SaveChangesAsync();
        check("expiry keyset continues beyond one bounded page",await Sweep()==201&&await Sweep()==0);


    }
}
