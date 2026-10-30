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
    public DbSet<TrustedContact> TrustedContacts => Set<TrustedContact>();
    public DbSet<MeetingSafetySession> MeetingSafetySessions => Set<MeetingSafetySession>();
    public DbSet<MeetingSafetyRecipient> MeetingSafetyRecipients => Set<MeetingSafetyRecipient>();
    public DbSet<MeetingSafetyLink> MeetingSafetyLinks => Set<MeetingSafetyLink>();
    public DbSet<MeetingSafetyCheckIn> MeetingSafetyCheckIns => Set<MeetingSafetyCheckIn>();
    public DbSet<MeetingSafetyEvent> MeetingSafetyEvents => Set<MeetingSafetyEvent>();
    public DbSet<MeetingSafetyOutbox> MeetingSafetyOutbox => Set<MeetingSafetyOutbox>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<EmailRetirementOperation> EmailRetirementOperations => Set<EmailRetirementOperation>();
    public DbSet<EmailRetirementReview> EmailRetirementReviews => Set<EmailRetirementReview>();
    public DbSet<BannedIdentifier> BannedIdentifiers => Set<BannedIdentifier>();
    public DbSet<BanDispositionReview> BanDispositionReviews => Set<BanDispositionReview>();
    public DbSet<BanDisposition> BanDispositions => Set<BanDisposition>();
    public DbSet<BanTransition> BanTransitions => Set<BanTransition>();
    public DbSet<BanCaseNote> BanCaseNotes => Set<BanCaseNote>();
    public DbSet<BanCheckAttempt> BanCheckAttempts => Set<BanCheckAttempt>();
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
    public DbSet<HomeLocationCheck> HomeLocationChecks => Set<HomeLocationCheck>();
    public DbSet<DobReview> DobReviews => Set<DobReview>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageHide> MessageHides => Set<MessageHide>();
    public DbSet<ConversationHide> ConversationHides => Set<ConversationHide>();
    public DbSet<Itinerary> Itineraries => Set<Itinerary>();
    public DbSet<IdVerificationAttempt> IdVerificationAttempts => Set<IdVerificationAttempt>();
    public DbSet<DiditWebhookEvent> DiditWebhookEvents => Set<DiditWebhookEvent>();
    public DbSet<ReportCategory> ReportCategories => Set<ReportCategory>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ModerationCase> ModerationCases => Set<ModerationCase>();
    public DbSet<CaseEvidence> CaseEvidence => Set<CaseEvidence>();
    public DbSet<Sanction> Sanctions => Set<Sanction>();
    public DbSet<Appeal> Appeals => Set<Appeal>();
    public DbSet<MemberNotice> MemberNotices => Set<MemberNotice>();
    public DbSet<StaffMemberLink> StaffMemberLinks => Set<StaffMemberLink>();
    public DbSet<Entitlement> Entitlements => Set<Entitlement>();
    public DbSet<ProfileOption> ProfileOptions => Set<ProfileOption>();
    public DbSet<MemberIdentity> MemberIdentities => Set<MemberIdentity>();
    public DbSet<ModeFilter> ModeFilters => Set<ModeFilter>();
    public DbSet<DataExport> DataExports => Set<DataExport>();
    public DbSet<TerminatedEmailHold> TerminatedEmailHolds => Set<TerminatedEmailHold>();
    public DbSet<MemberPhone> MemberPhones => Set<MemberPhone>();
    public DbSet<PhoneCode> PhoneCodes => Set<PhoneCode>();
    public DbSet<ContactHistory> ContactHistories => Set<ContactHistory>();
    public DbSet<LegalHold> LegalHolds => Set<LegalHold>();
    public DbSet<LegalHoldScope> LegalHoldScopes => Set<LegalHoldScope>();
    public DbSet<EvidenceItem> EvidenceItems => Set<EvidenceItem>();
    public DbSet<CustodyEvent> CustodyEvents => Set<CustodyEvent>();
    public DbSet<EvidenceExport> EvidenceExports => Set<EvidenceExport>();
    public DbSet<Disclosure> Disclosures => Set<Disclosure>();
    public DbSet<VerifiedIdentity> VerifiedIdentities => Set<VerifiedIdentity>();
    public DbSet<IdentityAccess> IdentityAccesses => Set<IdentityAccess>();
    public DbSet<TerminatedPhoneHold> TerminatedPhoneHolds => Set<TerminatedPhoneHold>();
    public DbSet<LeCase> LeCases => Set<LeCase>();
    public DbSet<LeRequest> LeRequests => Set<LeRequest>();
    public DbSet<LeCasePerson> LeCasePeople => Set<LeCasePerson>();
    public DbSet<LeNotification> LeNotifications => Set<LeNotification>();
    public DbSet<LeTransfer> LeTransfers => Set<LeTransfer>();
    public DbSet<EvidenceRedaction> EvidenceRedactions => Set<EvidenceRedaction>();
    public DbSet<AccessGrant> AccessGrants => Set<AccessGrant>();
    public DbSet<SensitiveAccess> SensitiveAccessLog => Set<SensitiveAccess>();
    public DbSet<StaffExclusion> StaffExclusions => Set<StaffExclusion>();
    public DbSet<ExportApproval> ExportApprovals => Set<ExportApproval>();
    public DbSet<SecurityIncident> SecurityIncidents => Set<SecurityIncident>();
    public DbSet<SearchEvent> SearchEvents => Set<SearchEvent>();
    public DbSet<ProfileVersion> ProfileVersions => Set<ProfileVersion>();
    public DbSet<MediaEscalationDelivery> MediaEscalationDeliveries => Set<MediaEscalationDelivery>();
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<MediaPlacement> MediaPlacements => Set<MediaPlacement>();
    public DbSet<MediaReviewView> MediaReviewViews => Set<MediaReviewView>();
    public DbSet<SafetyCountry> SafetyCountries => Set<SafetyCountry>();
    public DbSet<SafetyResource> SafetyResources => Set<SafetyResource>();
    public DbSet<SafetyRevision> SafetyRevisions => Set<SafetyRevision>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<NotificationSettings> NotificationSettings => Set<NotificationSettings>();
    public DbSet<NotificationModeMute> NotificationModeMutes => Set<NotificationModeMute>();
    public DbSet<PushDevice> PushDevices => Set<PushDevice>();

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
            // The purge job finds due deletions and expired tombstones (D170).
            account.HasIndex(a => new { a.Status, a.DeletionDueAt });
        });
        model.Entity<EmailRetirementOperation>(operation =>
        {
            // Restricted intent survives source deletion; no cascading destruction of uncertain provider work.
            operation.HasIndex(o => o.AccountId).IsUnique();
            operation.HasIndex(o => new { o.State, o.NextAttemptAt });
            operation.HasIndex(o => o.CaseId);
            operation.HasOne<BannedIdentifier>().WithMany().HasForeignKey(o=>o.RegistryId).OnDelete(DeleteBehavior.Restrict);
            operation.HasOne<ModerationCase>().WithMany().HasForeignKey(o=>o.CaseId).OnDelete(DeleteBehavior.Restrict);
            operation.Property(o => o.Outcome).HasMaxLength(32);
        });
        model.Entity<EmailRetirementReview>(review =>
        {
            review.HasOne<EmailRetirementOperation>().WithMany().HasForeignKey(r=>r.OperationId).OnDelete(DeleteBehavior.Restrict);
            review.HasIndex(r=>new{r.OperationId,r.ReviewedAt});
            review.Property(r=>r.Reason).HasMaxLength(BannedIdentifierPolicy.ReasonMax);
        });
        model.Entity<DataExport>(export =>
        {
            export.HasOne<Account>().WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Cascade);
            export.HasIndex(e => e.TokenHash).IsUnique();
            export.HasIndex(e => new { e.AccountId, e.CreatedAt });
        });
        model.Entity<MemberPhone>(phone =>
        {
            phone.HasKey(p => p.AccountId);
            phone.HasOne<Account>().WithOne().HasForeignKey<MemberPhone>(p => p.AccountId).OnDelete(DeleteBehavior.Cascade);
            phone.Property(p => p.Number).HasMaxLength(16);
            // DEFAULT (D181): one member account per verified number.
            phone.HasIndex(p => p.Number).IsUnique();
        });
        model.Entity<ContactHistory>(history =>
        {
            // D187: no foreign key, so a terminated member's history can outlive the tombstone's retention if needed.
            history.HasIndex(h => new { h.ContactType, h.LookupHmac });
            history.HasIndex(h => h.AccountId);
            history.HasIndex(h => h.RetainUntil);
            history.Property(h => h.ChangeReason).HasMaxLength(32);
        });
        // D189 (policy §3): legal holds and the restricted evidence store. No foreign keys to accounts or conversations,
        // so preserved evidence outlives its source; release resumes the independent review/police retention schedule.
        model.Entity<LegalHold>(hold =>
        {
            hold.HasIndex(h => h.SourceMediaId).IsUnique().HasFilter("source_media_id IS NOT NULL");
            hold.Property(h => h.Reason).HasMaxLength(LegalHoldPolicy.TextMax);
            hold.Property(h => h.ReleaseConditions).HasMaxLength(LegalHoldPolicy.TextMax);
            hold.Property(h => h.ReleaseReason).HasMaxLength(LegalHoldPolicy.TextMax);
            // Restricted append-only JSON-line disposition notes; each entry is validated to the text limit.
            hold.Property(h => h.Source).HasMaxLength(32);
            hold.HasIndex(h => new { h.Status, h.ReviewDue });
            hold.HasIndex(h => h.CaseId);
            hold.HasIndex(h => h.RetentionCaseIds).HasMethod("gin");
            hold.HasIndex(h => h.RelatedAccountIds).HasMethod("gin");
        });
        model.Entity<LegalHoldScope>(scope =>
        {
            scope.HasKey(s => new { s.HoldId, s.Kind, s.TargetId });
            scope.HasOne<LegalHold>().WithMany().HasForeignKey(s => s.HoldId).OnDelete(DeleteBehavior.Cascade);
            scope.HasIndex(s => s.TargetId);
        });
        model.Entity<EvidenceItem>(item =>
        {
            item.HasOne<LegalHold>().WithMany().HasForeignKey(i => i.HoldId).OnDelete(DeleteBehavior.Cascade);
            item.Property(i => i.Kind).HasMaxLength(32);
            item.Property(i => i.Source).HasMaxLength(64);
            item.Property(i => i.RecordId).HasMaxLength(64);
            item.HasIndex(i => new { i.HoldId, i.Kind, i.RecordId });
        });
        model.Entity<CustodyEvent>(custody =>
        {
            custody.HasKey(c => c.Sequence);
            custody.Property(c => c.Sequence).UseIdentityAlwaysColumn();
            custody.HasOne<LegalHold>().WithMany().HasForeignKey(c => c.HoldId).OnDelete(DeleteBehavior.Restrict);
            custody.Property(c => c.Action).HasMaxLength(32);
            custody.Property(c => c.Detail).HasMaxLength(256);
            custody.HasIndex(c => new { c.HoldId, c.Sequence });
        });
        model.Entity<EvidenceExport>(export =>
        {
            export.HasOne<LegalHold>().WithMany().HasForeignKey(e => e.HoldId).OnDelete(DeleteBehavior.Restrict);
            export.HasIndex(e => e.TokenHash).IsUnique();
            export.Property(e => e.Purpose).HasMaxLength(LegalHoldPolicy.TextMax);
        });
        model.Entity<Disclosure>(disclosure =>
        {
            disclosure.HasOne<LegalHold>().WithMany().HasForeignKey(d => d.HoldId).OnDelete(DeleteBehavior.Restrict);
            disclosure.Property(d => d.Agency).HasMaxLength(200);
            disclosure.Property(d => d.Officer).HasMaxLength(200);
            disclosure.Property(d => d.Reference).HasMaxLength(200);
            disclosure.Property(d => d.LegalBasis).HasMaxLength(32);
            disclosure.Property(d => d.VerifiedHow).HasMaxLength(200);
        });
        model.Entity<LegalHold>(hold =>
        {
            hold.Property(h => h.LawfulBasis).HasMaxLength(LawEnforcementPolicy.ShortMax);
            hold.Property(h => h.RequestingBody).HasMaxLength(LawEnforcementPolicy.ShortMax);
            hold.Property(h => h.Kind).HasMaxLength(32);
            hold.HasIndex(h => h.LeCaseId);
        });
        model.Entity<EvidenceExport>(export =>
        {
            export.Property(e => e.CorrectionReason).HasMaxLength(LegalHoldPolicy.TextMax);
            export.HasIndex(e => e.LeCaseId);
        });
        // D195 (procedure §14): restricted law-enforcement case management, in the `restricted` schema beside the identity
        // store (a separate database role when hosted, #89). No foreign keys to accounts, so records outlive deletion.
        model.Entity<LeCase>(item =>
        {
            item.ToTable("le_cases", "restricted");
            item.HasIndex(c => c.Reference).IsUnique();
            item.HasIndex(c => new { c.Status, c.ReceivedAt });
            item.HasIndex(c => c.RetainUntil);
            foreach (var name in new[] { nameof(LeCase.Reference), nameof(LeCase.Channel), nameof(LeCase.AgencyCountry), nameof(LeCase.VerificationSource), nameof(LeCase.LegalInstrument) })
                item.Property(name).HasMaxLength(32);
            foreach (var name in new[] { nameof(LeCase.Agency), nameof(LeCase.AgencyReference), nameof(LeCase.OfficerName), nameof(LeCase.OfficerRank),
                         nameof(LeCase.OfficerServiceNumber), nameof(LeCase.OfficerEmail), nameof(LeCase.OfficerPhone) })
                item.Property(name).HasMaxLength(LawEnforcementPolicy.ShortMax);
            foreach (var name in new[] { nameof(LeCase.VerificationEvidence), nameof(LeCase.LegalAssessment), nameof(LeCase.App65Note),
                         nameof(LeCase.ConfidentialityAssessment), nameof(LeCase.ClosureNote) })
                item.Property(name).HasMaxLength(LawEnforcementPolicy.TextMax);
        });
        model.Entity<LeRequest>(request =>
        {
            request.ToTable("le_requests", "restricted");
            request.HasOne<LeCase>().WithMany().HasForeignKey(r => r.CaseId).OnDelete(DeleteBehavior.Cascade);
            request.Property(r => r.Kind).HasMaxLength(32);
            request.Property(r => r.Status).HasMaxLength(32);
            request.Property(r => r.Description).HasMaxLength(LawEnforcementPolicy.TextMax);
            request.Property(r => r.DecisionReason).HasMaxLength(LawEnforcementPolicy.TextMax);
            request.Property(r => r.RetrospectiveNote).HasMaxLength(LawEnforcementPolicy.TextMax);
        });
        model.Entity<LeCasePerson>(person =>
        {
            person.ToTable("le_case_people", "restricted");
            person.HasKey(p => new { p.CaseId, p.AccountId });
            person.HasOne<LeCase>().WithMany().HasForeignKey(p => p.CaseId).OnDelete(DeleteBehavior.Cascade);
            person.Property(p => p.Kind).HasMaxLength(32);
            person.HasIndex(p => p.AccountId);
        });
        model.Entity<LeNotification>(notice =>
        {
            notice.ToTable("le_notifications", "restricted");
            notice.HasOne<LeCase>().WithMany().HasForeignKey(n => n.CaseId).OnDelete(DeleteBehavior.Cascade);
            notice.Property(n => n.Decision).HasMaxLength(16);
            notice.Property(n => n.Reason).HasMaxLength(LawEnforcementPolicy.TextMax);
            notice.HasIndex(n => new { n.CaseId, n.AccountId });
            notice.HasIndex(n => n.ReconsiderAt);
        });
        model.Entity<LeTransfer>(transfer =>
        {
            transfer.ToTable("le_transfers", "restricted");
            transfer.HasOne<LeCase>().WithMany().HasForeignKey(t => t.CaseId).OnDelete(DeleteBehavior.Cascade);
            transfer.Property(t => t.Recipient).HasMaxLength(LawEnforcementPolicy.ShortMax);
            transfer.Property(t => t.Method).HasMaxLength(32);
            transfer.Property(t => t.ReceiptReference).HasMaxLength(LawEnforcementPolicy.ShortMax);
        });
        // #88 (policy §11, §15): elevated access, the append-only sensitive-access log (a trigger refuses update and delete,
        // see the StaffAccess migration), conflict-of-interest exclusions and bulk export approvals. No foreign keys to accounts.
        model.Entity<AccessGrant>(grant =>
        {
            grant.Property(g => g.Capability).HasMaxLength(32);
            grant.Property(g => g.Purpose).HasMaxLength(StaffAccessPolicy.PurposeMax);
            grant.Property(g => g.RevokedReason).HasMaxLength(32);
            grant.HasIndex(g => new { g.AccountId, g.Capability, g.ExpiresAt });
        });
        model.Entity<SensitiveAccess>(entry =>
        {
            entry.ToTable("sensitive_access_log");
            entry.HasKey(e => e.Sequence);
            entry.Property(e => e.Sequence).UseIdentityAlwaysColumn();
            entry.Property(e => e.Action).HasMaxLength(16);
            entry.Property(e => e.Resource).HasMaxLength(32);
            entry.Property(e => e.ResourceId).HasMaxLength(64);
            entry.HasIndex(e => new { e.ActorAccountId, e.At });
            entry.HasIndex(e => new { e.Resource, e.ResourceId });
        });
        model.Entity<BanCheckAttempt>(attempt =>
        {
            attempt.HasIndex(a => new { a.CallerHash, a.KeyVersion, a.IdentifierType, a.At });
            attempt.HasIndex(a => new { a.AddressHash, a.KeyVersion, a.At });
            attempt.HasIndex(a => a.At);
            attempt.HasIndex(a => a.MatchedBanIds).HasMethod("gin");
            attempt.ToTable(t => t.HasCheckConstraint("ck_ban_attempt_hash", "octet_length(caller_hash) = 32 AND (address_hash IS NULL OR octet_length(address_hash) = 32) AND key_version > 0"));
        });
        model.Entity<BannedIdentifier>(ban =>
        {
            ban.Property(b => b.BanReasonCode).HasMaxLength(64);
            ban.HasIndex(b => new { b.IdentifierType, b.KeyVersion, b.IdentifierHash });
            ban.HasIndex(b => new { b.Status, b.BanExpiresAt });
            ban.HasIndex(b => b.CaseId);
            // Opaque case provenance survives ordinary member deletion; no cascading account relationship.
            ban.HasOne<ModerationCase>().WithMany().HasForeignKey(b => b.CaseId).OnDelete(DeleteBehavior.Restrict);
            ban.ToTable(t =>
            {
                t.HasCheckConstraint("ck_ban_hash", "octet_length(identifier_hash) = 32 AND key_version > 0");
                t.HasCheckConstraint("ck_ban_kind", "identifier_type IN ('Email', 'Mobile') AND status IN ('Active', 'Revoked', 'Expired')");
                t.HasCheckConstraint("ck_ban_reason", "ban_reason_code ~ '^[a-z][a-z0-9-]{1,63}$'");
                t.HasCheckConstraint("ck_ban_count", "match_count >= 0");
                t.HasCheckConstraint("ck_ban_revoked", "status <> 'Revoked' OR (revoked_at IS NOT NULL AND revoked_by IS NOT NULL AND ban_expires_at IS NOT NULL)");
                t.HasCheckConstraint("ck_ban_expired", "status <> 'Expired' OR ban_expires_at IS NOT NULL");
            });
        });
        model.Entity<BanDispositionReview>(review=>
        {
            review.HasOne<BanDisposition>().WithMany().HasForeignKey(r=>r.DispositionId).OnDelete(DeleteBehavior.Restrict);
            review.Property(r=>r.Reason).HasMaxLength(BannedIdentifierPolicy.ReasonMax);
            review.HasIndex(r=>new{r.CaseId,r.At});
            review.ToTable(t=>t.HasCheckConstraint("ck_ban_disposition_review_reason","length(btrim(reason)) BETWEEN 1 AND 1000"));
        });
        model.Entity<BanDisposition>(receipt =>
        {
            receipt.HasOne<ModerationCase>().WithMany().HasForeignKey(r=>r.CaseId).OnDelete(DeleteBehavior.Restrict);
            receipt.HasIndex(r=>new{r.CaseId,r.IdentifierType});
            receipt.ToTable(t=>
            {
                t.HasCheckConstraint("ck_ban_disposition_kind","identifier_type IN ('Email','Mobile') AND status IN ('Revoked','Expired')");
                t.HasCheckConstraint("ck_ban_disposition_clock","history_until >= ((effective_end AT TIME ZONE 'UTC') + interval '24 months') AT TIME ZONE 'UTC' AND disposed_at >= history_until");
            });
        });
        model.Entity<BanTransition>(entry =>
        {
            entry.Property(e => e.Action).HasMaxLength(32);
            entry.Property(e => e.Reason).HasMaxLength(BannedIdentifierPolicy.ReasonMax);
            entry.Property(e => e.OldValues).HasColumnType("jsonb");
            entry.Property(e => e.NewValues).HasColumnType("jsonb");
            entry.HasOne<BannedIdentifier>().WithMany().HasForeignKey(e => e.BannedIdentifierId).OnDelete(DeleteBehavior.Restrict);
            entry.HasIndex(e => new { e.CaseId, e.At });
            entry.ToTable(t => t.HasCheckConstraint("ck_ban_transition_reason", "length(btrim(reason)) BETWEEN 1 AND 1000"));
        });
        model.Entity<BanCaseNote>(note =>
        {
            note.Property(n => n.Text).HasMaxLength(BannedIdentifierPolicy.ReasonMax);
            note.HasOne<BanTransition>().WithMany().HasForeignKey(n => n.TransitionId).OnDelete(DeleteBehavior.Restrict);
            note.HasIndex(n => n.TransitionId).IsUnique();
            note.HasIndex(n => new { n.CaseId, n.At });
        });
        model.Entity<StaffExclusion>(exclusion =>
        {
            exclusion.HasKey(e => new { e.StaffAccountId, e.SubjectAccountId });
            exclusion.Property(e => e.Reason).HasMaxLength(StaffAccessPolicy.PurposeMax);
            exclusion.HasIndex(e => e.SubjectAccountId);
        });
        model.Entity<ExportApproval>(approval =>
        {
            approval.HasOne<LegalHold>().WithMany().HasForeignKey(a => a.HoldId).OnDelete(DeleteBehavior.Restrict);
            approval.Property(a => a.Purpose).HasMaxLength(LegalHoldPolicy.TextMax);
        });
        // #90: search events (a day, for the hourly limits); #91: profile versions for moderation and holds.
        model.Entity<SearchEvent>(search =>
        {
            search.Property(s => s.Kind).HasMaxLength(16);
            search.HasIndex(s => new { s.AccountId, s.Kind, s.At });
            search.HasIndex(s => s.At);
        });
        model.Entity<ProfileVersion>(version =>
        {
            version.Property(v => v.ModeId).HasMaxLength(32);
            version.Property(v => v.ContentJson).HasMaxLength(8000);
            version.HasIndex(v => new { v.AccountId, v.ModeId, v.CapturedAt });
        });
        // D201, D203 (policy §5, §6): photos in quarantine, where they show, and staff views of uncertain ones. No foreign
        // keys to accounts, so a held or blocked item outlives the account it came from.
        model.Entity<MediaItem>(item =>
        {
            item.Property(i => i.ObjectKey).HasMaxLength(64);
            item.Property(i => i.PreservationSourceKey).HasMaxLength(64);
            item.Property(i => i.PreservationError).HasMaxLength(32);
            item.Property(i => i.ContentType).HasMaxLength(32);
            item.Property(i => i.LastScreenError).HasMaxLength(32);
            item.Property(i => i.Screener).HasMaxLength(32);
            item.Property(i => i.PolicyVersion).HasMaxLength(32);
            item.Property(i => i.Signals).HasMaxLength(2000);
            item.Property(i => i.ReviewReason).HasMaxLength(MediaPolicy.ReasonMax);
            item.HasIndex(i => new { i.AccountId, i.CreatedAt });
            item.HasIndex(i => new { i.Status, i.NextScreenAt });
            item.HasIndex(i => i.Sha256);
            item.Property(i => i.Purpose).HasMaxLength(16);
            item.HasIndex(i => i.ConversationId);
        });
        model.Entity<MediaEscalationDelivery>(delivery =>
        {
            delivery.HasKey(d => new { d.MediaId, d.Slot });
            delivery.Property(d => d.Slot).HasMaxLength(16);
            delivery.Property(d => d.Status).HasMaxLength(24);
            delivery.Property(d => d.ResolutionActor).HasMaxLength(32);
            delivery.Property(d => d.ResolutionNote).HasMaxLength(2000);
            delivery.HasOne<MediaItem>().WithMany().HasForeignKey(d => d.MediaId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<MediaPlacement>(placement =>
        {
            placement.HasKey(p => new { p.AccountId, p.Scope, p.Position });
            placement.Property(p => p.Scope).HasMaxLength(32);
            placement.HasOne<MediaItem>().WithMany().HasForeignKey(p => p.MediaId).OnDelete(DeleteBehavior.Cascade);
            placement.HasIndex(p => p.MediaId);
        });
        model.Entity<MediaReviewView>(view =>
        {
            view.Property(v => v.Purpose).HasMaxLength(MediaPolicy.PurposeMax);
            view.HasIndex(v => new { v.MediaId, v.At });
        });
        // #49: the notification centre (ids only), deliveries per channel, settings, muted modes and push devices.
        model.Entity<Notification>(notification =>
        {
            notification.Property(n => n.ModeId).HasMaxLength(32);
            notification.Property(n => n.DedupeKey).HasMaxLength(64);
            notification.HasOne<Account>().WithMany().HasForeignKey(n => n.AccountId).OnDelete(DeleteBehavior.Cascade);
            notification.HasIndex(n => new { n.AccountId, n.UpdatedAt });
            notification.HasIndex(n => new { n.AccountId, n.DedupeKey }).IsUnique().HasFilter("dedupe_key IS NOT NULL");
        });
        model.Entity<NotificationDelivery>(delivery =>
        {
            delivery.Property(d => d.Channel).HasMaxLength(16);
            delivery.Property(d => d.Status).HasMaxLength(16);
            delivery.HasOne<Notification>().WithMany().HasForeignKey(d => d.NotificationId).OnDelete(DeleteBehavior.Cascade);
            delivery.HasIndex(d => d.NotificationId);
        });
        model.Entity<NotificationSettings>(settings =>
        {
            settings.HasKey(s => s.AccountId);
            settings.Property(s => s.TimeZone).HasMaxLength(64);
            settings.HasOne<Account>().WithMany().HasForeignKey(s => s.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<NotificationModeMute>(mute =>
        {
            mute.HasKey(m => new { m.AccountId, m.ModeId });
            mute.Property(m => m.ModeId).HasMaxLength(32);
            mute.HasOne<Account>().WithMany().HasForeignKey(m => m.AccountId).OnDelete(DeleteBehavior.Cascade);
            mute.HasOne<ConnectionMode>().WithMany().HasForeignKey(m => m.ModeId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<PushDevice>(device =>
        {
            device.Property(d => d.Platform).HasMaxLength(16);
            device.Property(d => d.Token).HasMaxLength(NotificationPolicy.TokenMax);
            device.HasOne<Account>().WithMany().HasForeignKey(d => d.AccountId).OnDelete(DeleteBehavior.Cascade);
            device.HasIndex(d => new { d.AccountId, d.Token }).IsUnique();
            device.HasIndex(d => d.InstallationId).IsUnique().HasFilter("installation_id IS NOT NULL");
        });
        // #103: country-aware Safety & Support resources as data, with an immutable revision trail.
        model.Entity<SafetyCountry>(country =>
        {
            country.HasKey(c => c.Code);
            country.Property(c => c.Code).HasMaxLength(2);
            country.Property(c => c.Name).HasMaxLength(SafetyPolicy.NameMax);
            country.Property(c => c.Owner).HasMaxLength(SafetyPolicy.NameMax);
            country.Property(c => c.Notes).HasMaxLength(SafetyPolicy.NotesMax);
            country.HasData(SafetySeed.Countries);
        });
        model.Entity<SafetyResource>(resource =>
        {
            resource.HasOne<SafetyCountry>().WithMany().HasForeignKey(r => r.CountryCode).OnDelete(DeleteBehavior.Restrict);
            resource.Property(r => r.CountryCode).HasMaxLength(2);
            resource.Property(r => r.RegionCode).HasMaxLength(10);
            resource.Property(r => r.LanguageCode).HasMaxLength(8);
            resource.Property(r => r.Name).HasMaxLength(SafetyPolicy.NameMax);
            resource.Property(r => r.Description).HasMaxLength(SafetyPolicy.DescriptionMax);
            resource.Property(r => r.Phone).HasMaxLength(SafetyPolicy.PhoneMax);
            resource.Property(r => r.PhoneDisplay).HasMaxLength(SafetyPolicy.PhoneMax);
            resource.Property(r => r.WebsiteUrl).HasMaxLength(SafetyPolicy.UrlMax);
            resource.Property(r => r.SourceUrl).HasMaxLength(SafetyPolicy.UrlMax);
            resource.HasIndex(r => new { r.CountryCode, r.Enabled, r.DisplayOrder });
            resource.HasData(SafetySeed.Resources);
        });
        model.Entity<SafetyRevision>(revision =>
        {
            revision.Property(r => r.CountryCode).HasMaxLength(2);
            revision.Property(r => r.Action).HasMaxLength(32);
            revision.Property(r => r.SnapshotJson).HasMaxLength(4000);
            revision.HasIndex(r => new { r.CountryCode, r.At });
        });
        // #89 (policy §12): security incidents with severity levels and the NDB assessment.
        model.Entity<SecurityIncident>(incident =>
        {
            incident.Property(i => i.Kind).HasMaxLength(32);
            incident.Property(i => i.Summary).HasMaxLength(SecurityOperationsPolicy.SummaryMax);
            incident.Property(i => i.AssessmentReason).HasMaxLength(SecurityOperationsPolicy.SummaryMax);
            incident.HasIndex(i => new { i.Status, i.Severity });
            incident.HasIndex(i => new { i.Kind, i.SubjectStaffId, i.DetectedAt });
        });
        model.Entity<EvidenceRedaction>(redaction =>
        {
            redaction.HasOne<EvidenceExport>().WithMany().HasForeignKey(r => r.ExportId).OnDelete(DeleteBehavior.Restrict);
            redaction.Property(r => r.Fields).HasMaxLength(500);
            redaction.Property(r => r.Reason).HasMaxLength(LegalHoldPolicy.TextMax);
            redaction.HasIndex(r => r.ExportId);
        });
        // D191 (policy §2.3, §16): the verified identity store lives in its own `restricted` schema, ready for a separate
        // database role when hosted (#89). No foreign key, so a terminated member's record can outlive the tombstone.
        model.Entity<VerifiedIdentity>(identity =>
        {
            identity.ToTable("verified_identities", "restricted");
            identity.HasKey(i => i.AccountId);
            identity.Property(i => i.SessionId).HasMaxLength(64);
            identity.Property(i => i.Method).HasMaxLength(32);
            identity.Property(i => i.Decision).HasMaxLength(16);
            identity.Property(i => i.IssuingCountry).HasMaxLength(8);
            identity.HasIndex(i => i.MatchHmac);
            identity.HasIndex(i => i.RetainUntil);
        });
        model.Entity<IdentityAccess>(access =>
        {
            access.ToTable("identity_accesses", "restricted");
            access.Property(a => a.Purpose).HasMaxLength(VerifiedIdentityPolicy.PurposeMax);
            access.HasIndex(a => new { a.AccountId, a.At });
        });
        model.Entity<PhoneCode>(code =>
        {
            code.HasOne<Account>().WithMany().HasForeignKey(c => c.AccountId).OnDelete(DeleteBehavior.Cascade);
            code.Property(c => c.Number).HasMaxLength(16);
            code.HasIndex(c => new { c.AccountId, c.Number, c.CreatedAt });
            code.HasIndex(c => new { c.Number, c.CreatedAt });
            code.HasIndex(c => new { c.IpHash, c.CreatedAt });
        });
        model.Entity<TerminatedEmailHold>(hold =>
        {
            hold.HasKey(h => h.EmailHash);
            hold.HasIndex(h => h.ExpiresAt);
        });
        model.Entity<TerminatedPhoneHold>(hold =>
        {
            hold.HasKey(h => h.PhoneHash);
            hold.HasIndex(h => h.ExpiresAt);
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
            profile.HasIndex(p => p.FounderEligibleAt).HasFilter("founder_eligible_at IS NOT NULL AND founder_place IS NULL");
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
            setting.Property(m => m.About).HasMaxLength(ProfileTextPolicy.AboutMax);
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
            // D149: arrival keeps the time, the market and the method; never coordinates.
            trip.Property(t => t.ArrivalMarketId).HasMaxLength(32);
            trip.HasOne<Market>().WithMany().HasForeignKey(t => t.ArrivalMarketId).OnDelete(DeleteBehavior.SetNull);
        });
        // D166: manual date-of-birth reviews; at most one open per member. Ids and outcomes only, never a date.
        model.Entity<DobReview>(review =>
        {
            review.HasOne<Account>().WithMany().HasForeignKey(r => r.AccountId).OnDelete(DeleteBehavior.Cascade);
            review.HasOne<IdVerificationAttempt>().WithMany().HasForeignKey(r => r.AttemptId).OnDelete(DeleteBehavior.Cascade);
            review.Property(r => r.Reason).HasMaxLength(32);
            review.HasIndex(r => r.AccountId, "ix_dob_reviews_one_open").IsUnique().HasFilter("resolved_at IS NULL");
            review.HasIndex(r => r.CreatedAt);
        });
        // D143: one open conversation per pair; a block closes it and a later reconnection opens a new one.
        model.Entity<Conversation>(conversation =>
        {
            conversation.Property(c => c.ClosedReason).HasMaxLength(16);
            conversation.HasOne<Account>().WithMany().HasForeignKey(c => c.PairLow).OnDelete(DeleteBehavior.Cascade);
            conversation.HasOne<Account>().WithMany().HasForeignKey(c => c.PairHigh).OnDelete(DeleteBehavior.Cascade);
            conversation.HasIndex(c => new { c.PairLow, c.PairHigh }, "ix_conversations_one_open").IsUnique().HasFilter("closed_at IS NULL");
            conversation.HasIndex(c => c.PairHigh);
            conversation.ToTable(t => t.HasCheckConstraint("ck_conversations_pair", "pair_low < pair_high"));
        });
        model.Entity<Message>(message =>
        {
            message.HasOne<Conversation>().WithMany().HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
            message.HasOne<Account>().WithMany().HasForeignKey(m => m.SenderAccountId).OnDelete(DeleteBehavior.Cascade);
            message.Property(m => m.Body).HasMaxLength(MessagingPolicy.TextMax);
            message.HasIndex(m => new { m.ConversationId, m.SentAt });
            message.HasIndex(m => new { m.SenderAccountId, m.SentAt });
            message.HasIndex(m => m.MediaId);
        });
        model.Entity<MessageHide>(hide =>
        {
            hide.HasKey(h => new { h.MessageId, h.AccountId });
            hide.HasOne<Message>().WithMany().HasForeignKey(h => h.MessageId).OnDelete(DeleteBehavior.Cascade);
            hide.HasOne<Account>().WithMany().HasForeignKey(h => h.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<ConversationHide>(hide =>
        {
            hide.HasKey(h => new { h.ConversationId, h.AccountId });
            hide.HasOne<Conversation>().WithMany().HasForeignKey(h => h.ConversationId).OnDelete(DeleteBehavior.Cascade);
            hide.HasOne<Account>().WithMany().HasForeignKey(h => h.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        // D150, D153: home-location consistency checks keep the outcome only; never coordinates, IP addresses or places.
        model.Entity<HomeLocationCheck>(check =>
        {
            check.HasOne<Account>().WithMany().HasForeignKey(c => c.AccountId).OnDelete(DeleteBehavior.Cascade);
            check.Property(c => c.HomeMarketId).HasMaxLength(32);
            check.HasIndex(c => new { c.AccountId, c.CheckedAt });
        });
        // D128: one row per Didit session, outcome and operational metadata only. At most one open attempt per member.
        model.Entity<IdVerificationAttempt>(attempt =>
        {
            attempt.HasOne<Account>().WithMany().HasForeignKey(a => a.AccountId).OnDelete(DeleteBehavior.Cascade);
            attempt.Property(a => a.SessionId).HasMaxLength(64);
            attempt.Property(a => a.WorkflowId).HasMaxLength(64);
            attempt.Property(a => a.ProviderStatus).HasMaxLength(32);
            attempt.Property(a => a.Url).HasMaxLength(512);
            attempt.HasIndex(a => a.SessionId).IsUnique();
            attempt.HasIndex(a => a.AccountId, "ix_id_verification_attempts_one_open").IsUnique()
                .HasFilter("state IN ('Created', 'InProgress', 'InReview')");
        });
        // D152: durable webhook dedupe on Didit's event id.
        model.Entity<DiditWebhookEvent>(webhook =>
        {
            webhook.HasKey(w => w.EventId);
            webhook.Property(w => w.EventId).ValueGeneratedNever();
            webhook.Property(w => w.WebhookType).HasMaxLength(48);
            webhook.Property(w => w.SessionId).HasMaxLength(64);
            webhook.Property(w => w.Outcome).HasMaxLength(32);
        });
        // D131: report categories are data with priorities; threats and suspected underage are urgent. D190: the Serious
        // Safety Incident categories (all urgent) are offered only in their own pathway; "threats" moved there.
        model.Entity<ReportCategory>(category =>
        {
            category.Property(c => c.Id).HasMaxLength(32);
            category.Property(c => c.Label).HasMaxLength(80);
            category.HasData(
                new ReportCategory { Id = "threats", Label = "Threats to my safety", Priority = ReportPriority.Urgent, SortOrder = 1, Serious = true },
                new ReportCategory { Id = "sexual-assault", Label = "Sexual assault", Priority = ReportPriority.Urgent, SortOrder = 11, Serious = true },
                new ReportCategory { Id = "violence", Label = "Physical violence", Priority = ReportPriority.Urgent, SortOrder = 12, Serious = true },
                new ReportCategory { Id = "stalking", Label = "Stalking or being followed", Priority = ReportPriority.Urgent, SortOrder = 13, Serious = true },
                new ReportCategory { Id = "coercion", Label = "Coercion, extortion or sextortion", Priority = ReportPriority.Urgent, SortOrder = 14, Serious = true },
                new ReportCategory { Id = "child-exploitation", Label = "Child sexual exploitation or a suspected minor", Priority = ReportPriority.Urgent, SortOrder = 15, Serious = true },
                new ReportCategory { Id = "danger", Label = "Other immediate danger", Priority = ReportPriority.Urgent, SortOrder = 16, Serious = true },
                new ReportCategory { Id = "underage", Label = "Might be under 18", Priority = ReportPriority.Urgent, SortOrder = 2 },
                new ReportCategory { Id = "harassment", Label = "Harassment or bullying", Priority = ReportPriority.High, SortOrder = 3 },
                new ReportCategory { Id = "hate", Label = "Hate or discrimination", Priority = ReportPriority.High, SortOrder = 4 },
                new ReportCategory { Id = "sexual", Label = "Unwanted sexual content or behaviour", Priority = ReportPriority.High, SortOrder = 5 },
                new ReportCategory { Id = "scam", Label = "Scam, fraud or selling", Priority = ReportPriority.High, SortOrder = 6 },
                new ReportCategory { Id = "fake", Label = "Fake profile or impersonation", Priority = ReportPriority.Normal, SortOrder = 7 },
                new ReportCategory { Id = "spam", Label = "Spam", Priority = ReportPriority.Normal, SortOrder = 8 },
                new ReportCategory { Id = "inappropriate", Label = "Inappropriate profile content", Priority = ReportPriority.Normal, SortOrder = 9 },
                new ReportCategory { Id = "other", Label = "Something else", Priority = ReportPriority.Normal, SortOrder = 10 });
        });
        model.Entity<ModerationCase>(item =>
        {
            item.HasOne<Account>().WithMany().HasForeignKey(c => c.SubjectAccountId).OnDelete(DeleteBehavior.Cascade);
            // One open case per subject account: new reports join it (D131: duplicates consolidated).
            item.HasIndex(c => c.SubjectAccountId, "ix_moderation_cases_one_open").IsUnique().HasFilter("status IN ('Open', 'UnderReview')");
            item.HasIndex(c => new { c.Queue, c.Status, c.Priority });
            item.Property(c => c.SystemFlag).HasMaxLength(64);
        });
        model.Entity<Report>(report =>
        {
            report.HasOne<Account>().WithMany().HasForeignKey(r => r.ReporterAccountId).OnDelete(DeleteBehavior.Cascade);
            report.HasOne<Account>().WithMany().HasForeignKey(r => r.SubjectAccountId).OnDelete(DeleteBehavior.Cascade);
            report.HasOne<ReportCategory>().WithMany().HasForeignKey(r => r.CategoryId).OnDelete(DeleteBehavior.Restrict);
            report.HasOne<ModerationCase>().WithMany().HasForeignKey(r => r.CaseId).OnDelete(DeleteBehavior.Cascade);
            report.Property(r => r.SubjectRef).HasMaxLength(64);
            report.Property(r => r.CategoryId).HasMaxLength(32);
            report.Property(r => r.Text).HasMaxLength(ReportPolicy.TextMax);
            report.HasIndex(r => new { r.ReporterAccountId, r.CreatedAt });
            report.HasIndex(r => r.CaseId);
            report.Property(r => r.SupportReference).HasMaxLength(9);
            report.HasIndex(r => r.SupportReference).IsUnique().HasFilter("support_reference IS NOT NULL");
            report.ToTable(t => t.HasCheckConstraint("ck_reports_not_self", "reporter_account_id <> subject_account_id"));
        });
        model.Entity<CaseEvidence>(evidence =>
        {
            evidence.HasOne<ModerationCase>().WithMany().HasForeignKey(e => e.CaseId).OnDelete(DeleteBehavior.Cascade);
            evidence.HasOne<Report>().WithMany().HasForeignKey(e => e.ReportId).OnDelete(DeleteBehavior.Cascade);
            evidence.Property(e => e.Kind).HasMaxLength(32);
            evidence.Property(e => e.Snapshot).HasMaxLength(8192);
            evidence.HasIndex(e => e.MediaIds).HasMethod("gin");
        });
        model.Entity<Sanction>(sanction =>
        {
            sanction.HasOne<Account>().WithMany().HasForeignKey(s => s.AccountId).OnDelete(DeleteBehavior.Cascade);
            sanction.HasOne<ModerationCase>().WithMany().HasForeignKey(s => s.CaseId).OnDelete(DeleteBehavior.SetNull);
            sanction.Property(s => s.Reason).HasMaxLength(ModerationPolicy.ReasonMax);
            sanction.HasIndex(s => new { s.AccountId, s.LiftedAt });
            sanction.ToTable(t => t.HasCheckConstraint("ck_sanctions_dates", "ends_at IS NULL OR ends_at > starts_at"));
        });
        model.Entity<Appeal>(appeal =>
        {
            appeal.HasOne<Sanction>().WithMany().HasForeignKey(a => a.SanctionId).OnDelete(DeleteBehavior.Cascade);
            appeal.HasOne<Account>().WithMany().HasForeignKey(a => a.AccountId).OnDelete(DeleteBehavior.Cascade);
            appeal.HasIndex(a => a.SanctionId).IsUnique(); // D135: one appeal per action
            appeal.Property(a => a.Text).HasMaxLength(ReportPolicy.TextMax);
        });
        model.Entity<MemberNotice>(notice =>
        {
            notice.HasOne<Account>().WithMany().HasForeignKey(n => n.AccountId).OnDelete(DeleteBehavior.Cascade);
            notice.HasOne<Sanction>().WithMany().HasForeignKey(n => n.SanctionId).OnDelete(DeleteBehavior.Cascade);
            notice.HasIndex(n => new { n.AccountId, n.CreatedAt });
        });
        model.Entity<StaffMemberLink>(link =>
        {
            link.HasKey(l => l.StaffAccountId);
            link.HasOne<Account>().WithMany().HasForeignKey(l => l.StaffAccountId).OnDelete(DeleteBehavior.Cascade);
            link.HasOne<Account>().WithMany().HasForeignKey(l => l.MemberAccountId).OnDelete(DeleteBehavior.Cascade);
            link.HasIndex(l => l.MemberAccountId).IsUnique();
        });
        model.Entity<Entitlement>(entitlement =>
        {
            entitlement.HasOne<Account>().WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Cascade);
            entitlement.Property(e => e.Reason).HasMaxLength(EntitlementPolicy.ReasonMax);
            entitlement.HasIndex(e => new { e.AccountId, e.RevokedAt });
            entitlement.ToTable(t => t.HasCheckConstraint("ck_entitlements_dates", "ends_at IS NULL OR ends_at > starts_at"));
        });
        // D138: gender and orientation option lists are data with stable ids.
        model.Entity<ProfileOption>(option =>
        {
            option.HasKey(o => new { o.Kind, o.Id });
            option.Property(o => o.Kind).HasMaxLength(16);
            option.Property(o => o.Id).HasMaxLength(32);
            option.Property(o => o.Label).HasMaxLength(60);
            option.HasData(
                new ProfileOption { Kind = "gender", Id = "man", Label = "Man", SortOrder = 1 },
                new ProfileOption { Kind = "gender", Id = "woman", Label = "Woman", SortOrder = 2 },
                new ProfileOption { Kind = "gender", Id = "non-binary", Label = "Non-binary", SortOrder = 3 },
                new ProfileOption { Kind = "gender", Id = "another", Label = "Another gender", SortOrder = 4 },
                new ProfileOption { Kind = "gender", Id = "unsaid", Label = "Prefer not to say", SortOrder = 5 },
                new ProfileOption { Kind = "orientation", Id = "straight", Label = "Straight", SortOrder = 1 },
                new ProfileOption { Kind = "orientation", Id = "gay", Label = "Gay", SortOrder = 2 },
                new ProfileOption { Kind = "orientation", Id = "lesbian", Label = "Lesbian", SortOrder = 3 },
                new ProfileOption { Kind = "orientation", Id = "bisexual", Label = "Bisexual", SortOrder = 4 },
                new ProfileOption { Kind = "orientation", Id = "pansexual", Label = "Pansexual", SortOrder = 5 },
                new ProfileOption { Kind = "orientation", Id = "asexual", Label = "Asexual", SortOrder = 6 },
                new ProfileOption { Kind = "orientation", Id = "queer", Label = "Queer", SortOrder = 7 },
                new ProfileOption { Kind = "orientation", Id = "questioning", Label = "Questioning", SortOrder = 8 },
                new ProfileOption { Kind = "orientation", Id = "another", Label = "Another orientation", SortOrder = 9 });
        });
        model.Entity<MemberIdentity>(identity =>
        {
            identity.HasKey(i => i.AccountId);
            identity.HasOne<Account>().WithOne().HasForeignKey<MemberIdentity>(i => i.AccountId).OnDelete(DeleteBehavior.Cascade);
            identity.Property(i => i.GenderId).HasMaxLength(32);
            identity.Property(i => i.OrientationId).HasMaxLength(32);
            identity.Property(i => i.GenderDescription).HasMaxLength(MatchPolicy.TextMax);
            identity.Property(i => i.Pronouns).HasMaxLength(MatchPolicy.TextMax);
        });
        model.Entity<ModeFilter>(filter =>
        {
            filter.HasKey(f => new { f.AccountId, f.ModeId });
            filter.Property(f => f.ModeId).HasMaxLength(32);
            filter.HasOne<Account>().WithMany().HasForeignKey(f => f.AccountId).OnDelete(DeleteBehavior.Cascade);
            filter.HasOne<ConnectionMode>().WithMany().HasForeignKey(f => f.ModeId).OnDelete(DeleteBehavior.Restrict);
            filter.ToTable(t => t.HasCheckConstraint("ck_mode_filters_ages", "(min_age IS NULL OR min_age >= 18) AND (max_age IS NULL OR max_age <= 120) AND (min_age IS NULL OR max_age IS NULL OR min_age <= max_age)"));
        });
        model.Entity<FounderPlace>(place =>
        {
            place.HasKey(p => p.Place);
            place.Property(p => p.Place).ValueGeneratedNever();
            // D130: never more than 1,000 places, whatever the configuration says.
            place.ToTable(t => t.HasCheckConstraint("ck_founder_places_place", "place >= 1 AND place <= 1000"));
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
        model.Entity<TrustedContact>(c=>
        {
            c.Property(x=>x.DisplayName).HasMaxLength(80);c.Property(x=>x.PreferredFirstName).HasMaxLength(50);c.Property(x=>x.Channel).HasMaxLength(8);c.Property(x=>x.InvitationStatus).HasMaxLength(24);
            c.ToTable(t=>{t.HasCheckConstraint("ck_trusted_contact_hash","octet_length(destination_lookup_hash)=32 AND octet_length(destination_encrypted)>=29 AND (consent_token_hash IS NULL OR octet_length(consent_token_hash)=32) AND (management_token_hash IS NULL OR octet_length(management_token_hash)=32)");t.HasCheckConstraint("ck_trusted_contact_channel","channel IN ('email','sms')");});
            c.HasIndex(x=>new{x.MemberId,x.CreatedAt});c.HasIndex(x=>x.DestinationLookupHash);
            c.HasIndex(x=>x.ConsentTokenHash).IsUnique().HasFilter("consent_token_hash IS NOT NULL");
            c.HasIndex(x=>x.ManagementTokenHash).IsUnique().HasFilter("management_token_hash IS NOT NULL");
        });
        model.Entity<MeetingSafetySession>(s=>
        {
            s.Property(x=>x.PreferredFirstName).HasMaxLength(50);s.Property(x=>x.TimeZoneId).HasMaxLength(100);
            s.Property(x=>x.VenueOrArea).HasMaxLength(160);s.Property(x=>x.MemberNote).HasMaxLength(500);s.Property(x=>x.PrivateFeedback).HasMaxLength(1000);
            s.ToTable(t=>t.HasCheckConstraint("ck_meeting_safety_clocks","final_check_in_at>starts_at AND ((closed_at IS NULL AND purge_after IS NULL) OR (closed_at IS NOT NULL AND purge_after=closed_at+interval '720 hours'))"));
            s.HasIndex(x=>new{x.MemberId,x.CreatedAt});s.HasIndex(x=>new{x.ClosedAt,x.FinalCheckInAt});s.HasIndex(x=>x.PurgeAfter);
        });
        model.Entity<MeetingSafetyRecipient>(r=>
        {
            r.HasIndex(x=>new{x.SessionId,x.TrustedContactId}).IsUnique();r.Property(x=>x.NotificationStatus).HasMaxLength(24);
            r.HasOne<MeetingSafetySession>().WithMany().HasForeignKey(x=>x.SessionId).OnDelete(DeleteBehavior.Cascade);
            r.HasOne<TrustedContact>().WithMany().HasForeignKey(x=>x.TrustedContactId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<MeetingSafetyLink>(l=>
        {
            l.ToTable(t=>t.HasCheckConstraint("ck_meeting_safety_token","octet_length(token_hash)=32 AND expires_at>created_at AND expires_at<=created_at+interval '720 hours'"));
            l.HasIndex(x=>x.TokenHash).IsUnique();l.HasIndex(x=>x.ExpiresAt);
            l.HasOne<MeetingSafetyRecipient>().WithMany().HasForeignKey(x=>x.RecipientId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<MeetingSafetyCheckIn>(c=>
        {
            c.ToTable(t=>t.HasCheckConstraint("ck_meeting_safety_grace","grace_ends_at=due_at+interval '30 minutes' AND (escalated_at IS NULL OR escalated_at>=grace_ends_at)"));
            c.Property(x=>x.Response).HasMaxLength(24);c.HasIndex(x=>new{x.SessionId,x.DueAt}).IsUnique();c.HasIndex(x=>new{x.GraceEndsAt,x.EscalatedAt});
            c.HasOne<MeetingSafetySession>().WithMany().HasForeignKey(x=>x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<MeetingSafetyEvent>(e=>
        {
            e.Property(x=>x.Kind).HasMaxLength(32);e.HasIndex(x=>new{x.SessionId,x.Id});
            e.HasIndex(x=>new{x.SessionId,x.CommandId}).IsUnique().HasFilter("command_id IS NOT NULL");
            e.HasOne<MeetingSafetySession>().WithMany().HasForeignKey(x=>x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<MeetingSafetyOutbox>(o=>
        {
            o.Property(x=>x.Kind).HasMaxLength(32);o.Property(x=>x.Status).HasMaxLength(24);o.Property(x=>x.DedupeKey).HasMaxLength(200);
            o.HasIndex(x=>x.DedupeKey).IsUnique();o.HasIndex(x=>new{x.Status,x.NextAttemptAt});o.HasIndex(x=>x.MemberId);
        });
        model.Entity<MeetingSafetyContactChallenge>(c=>
        {
            c.HasOne<MeetingSafetyRecipient>().WithMany().HasForeignKey(x=>x.RecipientId).OnDelete(DeleteBehavior.Cascade);
            c.HasIndex(x=>new{x.RecipientId,x.CreatedAt});c.HasIndex(x=>new{x.DestinationHash,x.CreatedAt});
            c.ToTable(t=>t.HasCheckConstraint("ck_meeting_contact_challenge","octet_length(destination_hash)=32 AND octet_length(code_hash)=32 AND expires_at>created_at AND expires_at<=created_at+interval '5 minutes' AND failed_attempts BETWEEN 0 AND 5"));
        });
        model.Entity<MeetingSafetyContactReadGrant>(g=>
        {
            g.HasOne<MeetingSafetyRecipient>().WithMany().HasForeignKey(x=>x.RecipientId).OnDelete(DeleteBehavior.Cascade);
            g.HasIndex(x=>x.TokenHash).IsUnique();g.HasIndex(x=>x.RecipientId);
            g.ToTable(t=>t.HasCheckConstraint("ck_meeting_contact_read_grant","octet_length(token_hash)=32 AND expires_at>created_at AND expires_at<=created_at+interval '4 hours'"));
        });
        model.AddReportOutcomes();
        model.AddModerationTransparency();
        model.AddPolicyNotices();
        model.Entity<BetaJourneyDay>(b=>
        {
            b.ToTable("beta_journey_days");b.HasKey(x=>new{x.AccountId,x.Day,x.Dataset});b.Property(x=>x.Dataset).HasMaxLength(24);
            b.HasOne<Account>().WithMany().HasForeignKey(x=>x.AccountId).OnDelete(DeleteBehavior.Cascade);b.HasIndex(x=>x.Day);
            b.ToTable(t=>t.HasCheckConstraint("ck_beta_journey_day","dataset IN ('unknown','local-synthetic','production') AND (NOT explore_has_peer OR explore_succeeded)"));
        });
        var acceptance=model.Entity<PolicyAcceptance>();acceptance.ToTable("policy_acceptances");acceptance.HasKey(a=>new{a.AccountId,a.Version});
        acceptance.Property(a=>a.Version).HasMaxLength(40);acceptance.Property(a=>a.ContentSha256).HasMaxLength(64);
        acceptance.HasOne<Account>().WithMany().HasForeignKey(a=>a.AccountId).OnDelete(DeleteBehavior.Cascade);
        acceptance.ToTable(t=>t.HasCheckConstraint("ck_policy_acceptance","version ~ '^[A-Za-z0-9][A-Za-z0-9.-]{0,39}$' AND content_sha256 ~ '^[0-9a-f]{64}$'"));
        model.AddModerationInspectionTotals();
        model.Entity<TextHarmIntent>(i=>
        {
            i.Property(x=>x.ModeId).HasMaxLength(32);i.Property(x=>x.Result).HasMaxLength(32);
            i.HasIndex(x=>new{x.ProcessedAt,x.CreatedAt});i.HasIndex(x=>x.AccountId);
            i.HasIndex(x=>new{x.AccountId,x.Source,x.SourceId,x.Revision}).IsUnique();
            i.ToTable(t=>t.HasCheckConstraint("ck_text_harm_intent","(processed_at IS NULL AND sealed_text IS NOT NULL AND octet_length(sealed_text)>=29 AND result IS NULL) OR (processed_at IS NOT NULL AND sealed_text IS NULL AND result IS NOT NULL)"));
        });
        model.Entity<TextHarmReview>(r=>
        {
            r.Property(x=>x.ModeId).HasMaxLength(32);r.Property(x=>x.SourceRevision).HasMaxLength(36);
            r.Property(x=>x.DetectorVersion).HasMaxLength(80);r.Property(x=>x.Outcome).HasMaxLength(32);
            r.HasIndex(x=>x.IntentId).IsUnique();r.HasIndex(x=>x.CaseId);r.HasIndex(x=>x.SubjectAccountId);
            r.HasOne<ModerationCase>().WithMany().HasForeignKey(x=>x.CaseId).OnDelete(DeleteBehavior.Restrict);
            r.ToTable(t=>t.HasCheckConstraint("ck_text_harm_review","(sealed_evidence IS NOT NULL AND octet_length(sealed_evidence)>=29 AND evidence_disposed_at IS NULL) OR (sealed_evidence IS NULL AND evidence_disposed_at IS NOT NULL)"));
        });
        model.Entity<ContentRemoval>(r=>
        {
            r.Property(x=>x.Field).HasMaxLength(24);r.Property(x=>x.ModeId).HasMaxLength(32);
            r.HasIndex(x=>x.CaseId);r.HasIndex(x=>x.SubjectAccountId);r.HasIndex(x=>x.MessageId);
            r.HasOne<ModerationCase>().WithMany().HasForeignKey(x=>x.CaseId).OnDelete(DeleteBehavior.Restrict);
            r.HasOne<Report>().WithMany().HasForeignKey(x=>x.ReportId).OnDelete(DeleteBehavior.Restrict);
            r.ToTable(t=>t.HasCheckConstraint("ck_content_removal_evidence","(sealed_snapshot IS NOT NULL AND octet_length(snapshot_hash)=32 AND octet_length(sealed_snapshot)>=29 AND evidence_disposed_at IS NULL) OR (sealed_snapshot IS NULL AND octet_length(snapshot_hash)=0 AND evidence_disposed_at IS NOT NULL AND evidence_disposed_at>=removed_at)"));
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
