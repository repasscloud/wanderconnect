using Microsoft.EntityFrameworkCore;
using Npgsql;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

/// <summary>Throwaway PostgreSQL: bounded disposal is a real database rule, never a writable session flag.</summary>
static class RegistryDispositionPersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options,Action<string,bool> check)
    {
        await using var db=new SwoovrDbContext(options);
        var now=new DateTimeOffset(DateTimeOffset.UtcNow.UtcTicks/10*10,TimeSpan.Zero);
        var owner=Guid.NewGuid();var staff=Guid.NewGuid();var review=Guid.NewGuid();
        db.Accounts.Add(new Account{Id=owner,Issuer="https://issuer.example.test",Subject="fictional-disposition-"+owner,Realm=AccountRealm.Members,Status=AccountStatus.Active,CreatedAt=now.AddYears(-4),LastSeenAt=now});
        db.ModerationCases.Add(new ModerationCase{Id=review,SubjectAccountId=owner,Status=CaseStatus.Closed,Queue=CaseQueue.Admin,CreatedAt=now.AddYears(-4),UpdatedAt=now.AddYears(-4),ClosedAt=now.AddYears(-4),EvidencePurgedAt=now.AddYears(-3)});
        var issued=BannedIdentifierPolicy.Issue(BannedIdentifierType.Email,new byte[32],1,review,staff,now.AddYears(-4),"Fictional disposed source restriction.");
        var revoked=BannedIdentifierPolicy.Revoke(issued.Entry,staff,"Fictional reviewed identifier-only release.",now.AddYears(-3));
        var sanction=new Sanction{Id=Guid.NewGuid(),AccountId=owner,CaseId=review,Kind=SanctionKind.Termination,Reason="Fictional independent original termination",StartsAt=now.AddYears(-4),CreatedBy=staff,CreatedByRole=AccountRole.Admin};
        db.BannedIdentifiers.Add(issued.Entry);db.BanTransitions.AddRange(issued.Transition,revoked.Transition);db.BanCaseNotes.AddRange(issued.Note,revoked.Note);db.Sanctions.Add(sanction);await db.SaveChangesAsync();
        async Task<bool> Refused(FormattableString sql){try{await db.Database.ExecuteSqlInterpolatedAsync(sql);return false;}catch(PostgresException error) when(error.MessageText.StartsWith("ban ")){return true;}}
        check("inactive history cannot be deleted without bounded disposition provenance",await Refused($"DELETE FROM ban_transitions WHERE banned_identifier_id={issued.Entry.Id}"));
        var hold=new LegalHold{Id=Guid.NewGuid(),Status=LegalHoldStatus.Active,Reason="Fictional related preservation",Source="staff",PlacedByRole=AccountRole.Legal,PlacedAt=now,ReviewDue=now.AddMonths(1),RelatedAccountIds=[owner],RetentionCaseIds=[]};
        db.LegalHolds.Add(hold);await db.SaveChangesAsync();
        check("database disposal eligibility honors related account holds",!await db.Database.SqlQuery<bool>($"SELECT ban_history_disposal_safe({issued.Entry.Id}) AS \"Value\"").SingleAsync());
        hold.RelatedAccountIds=null;await db.SaveChangesAsync();
        check("unknown hold membership fails closed in database predicate",!await db.Database.SqlQuery<bool>($"SELECT ban_history_disposal_safe({issued.Entry.Id}) AS \"Value\"").SingleAsync());
        hold.Status=LegalHoldStatus.Released;hold.RelatedAccountIds=[owner];await db.SaveChangesAsync();
        check("released hold without retained copies resumes database eligibility",await db.Database.SqlQuery<bool>($"SELECT ban_history_disposal_safe({issued.Entry.Id}) AS \"Value\"").SingleAsync());
        const string content="{\"fixture\":\"fictional retained registry source\"}";
        var copy=new EvidenceItem{Id=Guid.NewGuid(),HoldId=hold.Id,Capture=1,Kind="case",Source="moderation_cases",RecordId=review.ToString(),AccountId=owner,CapturedAt=now,
            Sha256=EvidenceSeal.Hash(content),Sealed=EvidenceSeal.Seal(1,new byte[32],content)};
        db.EvidenceItems.Add(copy);await db.SaveChangesAsync();
        check("released retained copies still protect related registry history",!await db.Database.SqlQuery<bool>($"SELECT ban_history_disposal_safe({issued.Entry.Id}) AS \"Value\"").SingleAsync());
        hold.RelatedAccountIds=null;await db.SaveChangesAsync();
        check("unknown released-copy membership refuses registry disposal",!await db.Database.SqlQuery<bool>($"SELECT ban_history_disposal_safe({issued.Entry.Id}) AS \"Value\"").SingleAsync());
        hold.RelatedAccountIds=[owner];db.EvidenceItems.Remove(copy);await db.SaveChangesAsync();
        check("fixture copy expiry resumes independent registry clock",await db.Database.SqlQuery<bool>($"SELECT ban_history_disposal_safe({issued.Entry.Id}) AS \"Value\"").SingleAsync());
        await using(var malformed=new SwoovrDbContext(options))
        {
            await using var tx=await malformed.Database.BeginTransactionAsync();
            var source=BannedIdentifierPolicy.Issue(BannedIdentifierType.Email,new byte[32],1,review,staff,now.AddYears(-4),"Fictional incomplete terminal provenance.");
            source.Entry.Status=BanStatus.Revoked;source.Entry.RevokedAt=now.AddYears(-3);source.Entry.BanExpiresAt=source.Entry.RevokedAt;source.Entry.RevokedBy=staff;
            malformed.BannedIdentifiers.Add(source.Entry);malformed.BanTransitions.Add(source.Transition);malformed.BanCaseNotes.Add(source.Note);await malformed.SaveChangesAsync();
            malformed.BanDispositions.Add(BannedIdentifierPolicy.Disposition(source.Entry,now,false,now.AddYears(-2)));
            var refused=false;try{await malformed.SaveChangesAsync();}catch(DbUpdateException){refused=true;}
            check("missing terminal release provenance cannot authorize disposal",refused);await tx.RollbackAsync();
        }
        await using(var legacy=new SwoovrDbContext(options))
        {
            await using var tx=await legacy.Database.BeginTransactionAsync();var source=await legacy.BannedIdentifiers.SingleAsync(b=>b.Id==issued.Entry.Id);
            var extra=BannedIdentifierPolicy.RecordReinstatement(source,staff,"Fictional later recorded review with stale row clock.",now);
            source.LastReviewedAt=issued.Entry.LastReviewedAt;legacy.BanTransitions.Add(extra.Transition);legacy.BanCaseNotes.Add(extra.Note);await legacy.SaveChangesAsync();
            check("later immutable review prevents disposal despite stale source clock",!await legacy.Database.SqlQuery<bool>($"SELECT ban_history_disposal_safe({source.Id}) AS \"Value\"").SingleAsync());
            await tx.RollbackAsync();
        }
        await using(var failed=new SwoovrDbContext(options))
        {
            await using var tx=await failed.Database.BeginTransactionAsync();
            failed.BanDispositions.Add(BannedIdentifierPolicy.Disposition(issued.Entry,now,false,now.AddYears(-2)));await failed.SaveChangesAsync();
            await failed.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ban_case_notes WHERE case_id={review}");
            await tx.RollbackAsync();
        }
        check("rollback retains notes and leaves no partial disposition",await db.BanCaseNotes.CountAsync(n=>n.CaseId==review)==2&&!await db.BanDispositions.AnyAsync(d=>d.Id==issued.Entry.Id));
        db.BanDispositions.Add(BannedIdentifierPolicy.Disposition(issued.Entry,now,false,now.AddYears(-2)));await db.SaveChangesAsync();
        var raceHoldId=Guid.NewGuid();
        await using(var writer=new SwoovrDbContext(options))
        await using(var deleting=new SwoovrDbContext(options))
        {
            await using var tx=await writer.Database.BeginTransactionAsync();
            await writer.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({Swoovr.Api.Retention.PreservationLease.BarrierNamespace},0)");
            writer.LegalHolds.Add(new LegalHold{Id=raceHoldId,Status=LegalHoldStatus.Active,Reason="Fictional concurrent preservation",Source="staff",PlacedByRole=AccountRole.Legal,PlacedAt=now,ReviewDue=now.AddMonths(1),RelatedAccountIds=[owner],RetentionCaseIds=[]});await writer.SaveChangesAsync();
            await deleting.Database.OpenConnectionAsync();var pid=((NpgsqlConnection)deleting.Database.GetDbConnection()).ProcessID;
            async Task<bool> Delete(){try{await deleting.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ban_case_notes WHERE case_id={review}");return false;}catch(PostgresException error) when(error.MessageText.Contains("append-only")){return true;}}
            var attempt=Delete();var waiting=false;
            for(var i=0;i<100;i++)
            {
                waiting=await db.Database.SqlQuery<bool>($"SELECT EXISTS(SELECT 1 FROM pg_locks WHERE pid={pid} AND locktype='advisory' AND NOT granted) AS \"Value\"").SingleAsync();
                if(waiting||attempt.IsCompleted)break;await Task.Delay(20);
            }
            check("direct SQL disposal waits for preservation statement authority before tuple work",waiting);
            await writer.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM ban_case_notes WHERE case_id={review} FOR UPDATE NOWAIT");
            check("waiting direct delete has not taken source tuple locks before barrier",true);
            await tx.CommitAsync();check("post-wait volatile guard observes newly committed hold",await attempt);
        }
        check("concurrent preservation leaves source notes and receipt intact",await db.BanCaseNotes.CountAsync(n=>n.CaseId==review)==2&&await db.BanDispositions.AnyAsync(d=>d.Id==issued.Entry.Id));
        await db.LegalHolds.Where(h=>h.Id==raceHoldId).ExecuteUpdateAsync(u=>u.SetProperty(h=>h.Status,LegalHoldStatus.Released));
        await using(var tx=await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ban_case_notes WHERE case_id={review}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ban_transitions WHERE banned_identifier_id={issued.Entry.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM banned_identifiers WHERE id={issued.Entry.Id}");
            await tx.CommitAsync();
        }
        check("bounded disposal removes fingerprint snapshots and text but retains minimal receipt",!await db.BannedIdentifiers.AnyAsync(b=>b.Id==issued.Entry.Id)&&!await db.BanTransitions.AnyAsync(t=>t.BannedIdentifierId==issued.Entry.Id)&&!await db.BanCaseNotes.AnyAsync(n=>n.CaseId==review)&&await db.BanDispositions.AnyAsync(d=>d.Id==issued.Entry.Id));
        check("minimal disposition cannot be rewritten",await Refused($"UPDATE ban_dispositions SET disposed_at=disposed_at WHERE id={issued.Entry.Id}"));
        check("ongoing original termination requires disposition continuity",await Refused($"DELETE FROM ban_dispositions WHERE id={issued.Entry.Id}")&&await db.Sanctions.AnyAsync(s=>s.Id==sanction.Id&&s.LiftedAt==null));
        await using(var stale=new SwoovrDbContext(options))
        {
            await using var tx=await stale.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
            var refused=false;try{await stale.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ban_dispositions WHERE id={issued.Entry.Id}");}
            catch(PostgresException error) when(error.MessageText.Contains("read committed isolation")){refused=true;}
            check("retained transaction snapshots cannot authorize disposal",refused);await tx.RollbackAsync();
        }
        sanction.LiftedAt=now;sanction.LiftedBy=staff;await db.SaveChangesAsync();
        foreach(var operation in new[]{"update","delete"})
        {
            await using var later=new SwoovrDbContext(options);await using var tx=await later.Database.BeginTransactionAsync();
            var minimal=await later.BanDispositions.SingleAsync(d=>d.Id==issued.Entry.Id);
            var note=BannedIdentifierPolicy.RecordReinstatement(minimal,staff,"Fictional new reinstatement review after fingerprint disposal.",now);
            later.BanDispositionReviews.Add(note);await later.SaveChangesAsync();
            check("new post-disposal review creates independent case retention clock",!await later.Database.SqlQuery<bool>($"SELECT ban_case_disposal_safe({review}) AS \"Value\"").SingleAsync());
            check("new review preserves original case closure history",await later.ModerationCases.Where(c=>c.Id==review).Select(c=>c.ClosedAt).SingleAsync()==now.AddYears(-4));
            var refused=false;
            try
            {
                if(operation=="update")await later.Database.ExecuteSqlInterpolatedAsync($"UPDATE ban_disposition_reviews SET reason=reason WHERE id={note.Id}");
                else await later.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ban_disposition_reviews WHERE id={note.Id}");
            }
            catch(PostgresException error) when(error.MessageText.Contains("ban disposition review")){refused=true;}
            check("post-disposal review refuses rewrite and premature erasure",refused);await tx.RollbackAsync();
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ban_dispositions WHERE id={issued.Entry.Id}");
        check("minimal disposition expires after enforcement and case obligations end",!await db.BanDispositions.AnyAsync(d=>d.Id==issued.Entry.Id));
        await db.LegalHolds.Where(h=>h.Id==hold.Id||h.Id==raceHoldId).ExecuteDeleteAsync();
        await db.Sanctions.Where(s=>s.Id==sanction.Id).ExecuteDeleteAsync();
        await db.ModerationCases.Where(c=>c.Id==review).ExecuteDeleteAsync();
        await db.Accounts.Where(a=>a.Id==owner).ExecuteDeleteAsync();
    }
}
