using Microsoft.EntityFrameworkCore;
using Npgsql;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

// Actual PostgreSQL triggers in an owned throwaway database; no shared fixture mutation.
static class ContentRemovalGuardChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options, Action<string,bool> check)
    {
        await using var source=new SwoovrDbContext(options);
        var original=source.Database.GetDbConnection().ConnectionString;
        await using var admin=new NpgsqlConnection(original);await admin.OpenAsync();
        var name="swoovr_content_guard_"+Guid.NewGuid().ToString("N");
        await using(var create=new NpgsqlCommand("CREATE DATABASE "+name,admin))await create.ExecuteNonQueryAsync();
        try
        {
            var connection=new NpgsqlConnectionStringBuilder(original){Database=name,Pooling=false}.ConnectionString;
            await using var db=new SwoovrDbContext(new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(connection).Options);
            await db.Database.MigrateAsync();
            var now=DateTimeOffset.UtcNow;var subject=Guid.NewGuid();var reporter=Guid.NewGuid();var staff=Guid.NewGuid();
            Account Account(Guid id,AccountRealm realm)=>new(){Id=id,Issuer="https://issuer.example.test/"+realm,Subject="fixture-"+id,
                Realm=realm,Status=AccountStatus.Active,CreatedAt=now.AddYears(-3),LastSeenAt=now,IsRootAdmin=id==staff};
            db.Accounts.AddRange(Account(subject,AccountRealm.Members),Account(reporter,AccountRealm.Members),Account(staff,AccountRealm.Staff));
            db.MemberProfiles.AddRange(new(){AccountId=subject,PublicId=Guid.NewGuid(),DisplayName="Fictional subject",AgeStatus=AgeStatus.Attested,DateOfBirth=new DateOnly(1990,1,1)},
                new(){AccountId=reporter,PublicId=Guid.NewGuid(),DisplayName="Fictional reporter",AgeStatus=AgeStatus.Attested,DateOfBirth=new DateOnly(1990,1,1)});
            var category=await db.ReportCategories.Select(c=>c.Id).FirstAsync();
            var review=new ModerationCase{Id=Guid.NewGuid(),SubjectAccountId=subject,Status=CaseStatus.Closed,Queue=CaseQueue.Admin,
                CreatedAt=now.AddMonths(-27),UpdatedAt=now.AddMonths(-25),ClosedAt=now.AddMonths(-25),ClosedBy=staff};
            var other=new ModerationCase{Id=Guid.NewGuid(),SubjectAccountId=subject,Status=CaseStatus.Closed,Queue=CaseQueue.Admin,
                CreatedAt=now.AddMonths(-27),UpdatedAt=now.AddMonths(-25),ClosedAt=now.AddMonths(-25),ClosedBy=staff};
            db.ModerationCases.AddRange(review,other);
            var report=new Report{Id=Guid.NewGuid(),CaseId=review.Id,ReporterAccountId=reporter,SubjectAccountId=subject,
                SubjectType=ReportSubject.Profile,SubjectRef=Guid.NewGuid().ToString(),CategoryId=category,CreatedAt=now.AddMonths(-27)};
            var otherReport=new Report{Id=Guid.NewGuid(),CaseId=other.Id,ReporterAccountId=reporter,SubjectAccountId=subject,
                SubjectType=ReportSubject.Profile,SubjectRef=Guid.NewGuid().ToString(),CategoryId=category,CreatedAt=now.AddMonths(-27)};
            db.Reports.AddRange(report,otherReport);await db.SaveChangesAsync();
            var payload="{\"text\":\"Fictional preserved content\",\"reason\":\"Fictional policy decision\"}";
            var key1=Enumerable.Repeat((byte)11,32).ToArray();var key2=Enumerable.Repeat((byte)22,32).ToArray();
            var removal=new ContentRemoval{Id=Guid.NewGuid(),CaseId=review.Id,ReportId=report.Id,SubjectAccountId=subject,ActorAccountId=staff,
                Field="about",RemovedAt=now.AddMonths(-26),SnapshotHash=EvidenceSeal.Hash(payload),SealedSnapshot=EvidenceSeal.Seal(1,key1,payload)};
            db.Set<ContentRemoval>().Add(removal);await db.SaveChangesAsync();
            var id=removal.Id;
            Task<int> Sql(string text)=>db.Database.ExecuteSqlRawAsync(text);
            async Task<bool> Refused(string text)
            {
                var transaction=db.Database.CurrentTransaction;
                if(transaction is not null)await transaction.CreateSavepointAsync("guard_probe");
                try{await Sql(text);if(transaction is not null)await transaction.ReleaseSavepointAsync("guard_probe");return false;}
                catch(PostgresException error)
                {
                    if(transaction is not null){await transaction.RollbackToSavepointAsync("guard_probe");await transaction.ReleaseSavepointAsync("guard_probe");}
                    return error.SqlState is "P0001" or "23514" or "23503";
                }
            }
            check("content decision metadata is immutable",await Refused($"UPDATE content_removal SET actor_account_id='{reporter}' WHERE id='{id}'"));
            check("content snapshot hash is immutable",await Refused($"UPDATE content_removal SET snapshot_hash=decode(repeat('00',32),'hex') WHERE id='{id}'"));
            check("content record truncate is refused",await Refused("TRUNCATE content_removal"));
            check("sealed decision cannot be deleted",await Refused($"DELETE FROM content_removal WHERE id='{id}'"));
            check("content case/report association is checked on insert",await Refused($"INSERT INTO content_removal SELECT gen_random_uuid(),'{other.Id}','{report.Id}',subject_account_id,actor_account_id,field,mode_id,message_id,removed_at,sealed_snapshot,snapshot_hash,evidence_disposed_at FROM content_removal WHERE id='{id}'"));
            check("invalid field shape is refused on insert",await Refused($"INSERT INTO content_removal SELECT gen_random_uuid(),case_id,report_id,subject_account_id,actor_account_id,'message',NULL,NULL,removed_at,sealed_snapshot,snapshot_hash,evidence_disposed_at FROM content_removal WHERE id='{id}'"));
            check("content evidence cannot disappear before approved case disposal",await Refused($"UPDATE content_removal SET sealed_snapshot=NULL,snapshot_hash=''::bytea,evidence_disposed_at=now() WHERE id='{id}'"));
            var rotated=EvidenceSeal.Seal(2,key2,payload);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE content_removal SET sealed_snapshot={rotated} WHERE id={id}");
            var read=await db.Set<ContentRemoval>().AsNoTracking().SingleAsync(r=>r.Id==id);
            check("content key rotation retains immutable hash and exact evidence",read.SnapshotHash.SequenceEqual(EvidenceSeal.Hash(payload))
                &&EvidenceSeal.Open(new Dictionary<byte,byte[]>{{2,key2}},read.SealedSnapshot!)==payload);
            check("same-version ciphertext rewrite is refused",await Refused($"UPDATE content_removal SET sealed_snapshot=set_byte(sealed_snapshot,5,42) WHERE id='{id}'"));
            review.EvidencePurgedAt=now;await db.SaveChangesAsync();
            await using var preservationFixture=await db.Database.BeginTransactionAsync();
            var hold=new LegalHold{Id=Guid.NewGuid(),CaseId=review.Id,Status=LegalHoldStatus.Active,PlacedAt=now,ReviewDue=now.AddDays(30),
                RelatedAccountIds=[subject],RetentionCaseIds=[review.Id],Reason="Fictional preservation",PlacedByRole=AccountRole.Legal};
            db.LegalHolds.Add(hold);await db.SaveChangesAsync();
            check("active hold prevents content erasure despite case flag",await Refused($"UPDATE content_removal SET sealed_snapshot=NULL,snapshot_hash=''::bytea,evidence_disposed_at=now() WHERE id='{id}'"));
            hold.Status=LegalHoldStatus.Released;hold.ReleasedAt=now;hold.ReleasedBy=staff;hold.ReleaseReason="Fictional release";
            var copy=new EvidenceItem{Id=Guid.NewGuid(),HoldId=hold.Id,Capture=1,Kind="profile",Source="member_profiles",RecordId=subject.ToString(),AccountId=subject,
                CapturedAt=now,CapturedBy=staff,Sha256=EvidenceSeal.Hash(payload),Sealed=EvidenceSeal.Seal(1,key1,payload)};
            db.EvidenceItems.Add(copy);await db.SaveChangesAsync();
            check("released retained-copy hold still protects content",await Refused($"UPDATE content_removal SET sealed_snapshot=NULL,snapshot_hash=''::bytea,evidence_disposed_at=now() WHERE id='{id}'"));
            await preservationFixture.RollbackAsync();await preservationFixture.DisposeAsync();db.ChangeTracker.Clear();
            await using var unknownFixture=await db.Database.BeginTransactionAsync();
            var unknown=new LegalHold{Id=Guid.NewGuid(),Status=LegalHoldStatus.Active,PlacedAt=now,ReviewDue=now.AddDays(30),Reason="Fictional unresolved scope",PlacedByRole=AccountRole.Legal};
            db.LegalHolds.Add(unknown);await db.SaveChangesAsync();
            check("unknown active hold membership prevents content erasure",await Refused($"UPDATE content_removal SET sealed_snapshot=NULL,snapshot_hash=''::bytea,evidence_disposed_at=now() WHERE id='{id}'"));
            // Roll back this test-owned insertion instead of bypassing immutable-copy guards.
            await unknownFixture.RollbackAsync();await unknownFixture.DisposeAsync();db.ChangeTracker.Clear();
            review=await db.ModerationCases.SingleAsync(c=>c.Id==review.Id);
            var sanction=new Sanction{Id=Guid.NewGuid(),AccountId=subject,CaseId=review.Id,Kind=SanctionKind.Warning,Reason="Fictional warning",StartsAt=now,CreatedBy=staff,CreatedByRole=AccountRole.Admin};
            var appeal=new Appeal{Id=Guid.NewGuid(),SanctionId=sanction.Id,AccountId=subject,Status=AppealStatus.Pending,CreatedAt=now};
            await using(var appealFixture=await db.Database.BeginTransactionAsync())
            {
                db.Sanctions.Add(sanction);db.Appeals.Add(appeal);await db.SaveChangesAsync();
                check("pending appeal prevents approved-flag erasure",await Refused($"UPDATE content_removal SET sealed_snapshot=NULL,snapshot_hash=''::bytea,evidence_disposed_at=now() WHERE id='{id}'"));
                await appealFixture.RollbackAsync();
            }
            db.ChangeTracker.Clear();review=await db.ModerationCases.SingleAsync(c=>c.Id==review.Id);
            // Recent closure proves content disposal is independent of the minimum24-month record clock.
            review.ClosedAt=now.AddMonths(-13);await db.SaveChangesAsync();
            await Sql($"UPDATE content_removal SET sealed_snapshot=NULL,snapshot_hash=''::bytea,evidence_disposed_at=now() WHERE id='{id}'");
            check("approved case-content flag permits bounded earlier content erasure",await db.Set<ContentRemoval>().AsNoTracking().AnyAsync(r=>r.Id==id&&r.SealedSnapshot==null&&r.SnapshotHash.Length==0&&r.EvidenceDisposedAt!=null));
            check("disposed evidence cannot be restored",await Refused($"UPDATE content_removal SET sealed_snapshot=decode('01'||repeat('00',28),'hex'),evidence_disposed_at=NULL WHERE id='{id}'"));
            check("content disposition time is immutable",await Refused($"UPDATE content_removal SET evidence_disposed_at=now()+interval '1 second' WHERE id='{id}'"));
            check("minimal decision stays for its separate record clock",await Refused($"DELETE FROM content_removal WHERE id='{id}'"));
            review.ClosedAt=now.AddMonths(-25);await db.SaveChangesAsync();
            await Sql($"DELETE FROM content_removal WHERE id='{id}'");
            check("disposed minimal decision deletes only after guarded case clock",!await db.Set<ContentRemoval>().AsNoTracking().AnyAsync(r=>r.Id==id));
        }
        finally
        {
            await using var drop=new NpgsqlCommand("DROP DATABASE "+name+" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();
        }
    }
}
