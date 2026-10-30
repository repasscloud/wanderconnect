using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Accounts;

/// <summary>
/// The member's data export (Q43 rules 21–25, Q64, D172), built when downloaded and never stored. Only what is theirs:
/// other members appear by display name only (as the member already sees them), never their private fields; reports
/// about the member, who filed them, staff notes, staff ids and internal market ids are left out. Deleted messages
/// appear as deleted, without text. Format: JSON, documented in docs/delivery/VALIDATION.md (slice V).
/// </summary>
public static class DataExportBuilder
{
    private const string DeletedMember = "Deleted member";

    public static async Task<object> BuildAsync(SwoovrDbContext db, Account account, DateTimeOffset now, CancellationToken cancellationToken,
        VerifiedIdentityFields? verifiedIdentity = null, IReadOnlyList<object>? photos = null, IReadOnlyList<object>? chatPhotos = null, object? meetingSafety = null)
    {
        var id = account.Id;
        var profile = await db.MemberProfiles.FindAsync([id], cancellationToken);
        var identity = await db.MemberIdentities.FindAsync([id], cancellationToken);
        var home = profile?.HomeGeoNameId is { } homeId ? await db.GeoCities.FindAsync([homeId], cancellationToken) : null;
        var chips = await (from c in db.MemberModeIntentions join i in db.ModeIntentions on c.IntentionId equals i.Id
                           where c.AccountId == id select new { c.ModeId, i.Label }).ToListAsync(cancellationToken);
        var modes = (await db.MemberModes.Where(m => m.AccountId == id).ToListAsync(cancellationToken))
            .Select(m => new { mode = m.ModeId, m.Enabled, m.Visible, intentions = chips.Where(c => c.ModeId == m.ModeId).Select(c => c.Label).ToList(), m.UpdatedAt });
        var filters = (await db.ModeFilters.Where(f => f.AccountId == id).ToListAsync(cancellationToken))
            .Select(f => new { mode = f.ModeId, f.MinAge, f.MaxAge, f.RequiredChips, f.RequiredMatch, f.ExcludedChips, f.IdVerifiedOnly, f.Visitors, f.MaxDistanceKm, f.Sort });
        var trips = await (from t in db.Trips join c in db.GeoCities on t.GeoNameId equals c.GeoNameId
                           where t.AccountId == id orderby t.Arrival
                           select new { city = c.Name, country = c.CountryName, t.Arrival, t.Departure, t.Visible, t.CancelledAt, t.ArrivedAt, t.CreatedAt }).ToListAsync(cancellationToken);

        var names = await NamesAsync(db, id, cancellationToken);
        string Name(Guid other) => names.GetValueOrDefault(other) ?? DeletedMember;
        var requests = (await db.ConnectionRequests.Where(r => r.FromAccountId == id || r.ToAccountId == id).OrderBy(r => r.CreatedAt).ToListAsync(cancellationToken))
            .Select(r => new { direction = r.FromAccountId == id ? "sent" : "received", with = Name(r.FromAccountId == id ? r.ToAccountId : r.FromAccountId),
                mode = r.ModeId, r.Status, r.CreatedAt, r.AnsweredAt });
        var blocks = (await db.Blocks.Where(b => b.BlockerAccountId == id).ToListAsync(cancellationToken)).Select(b => new { person = Name(b.BlockedAccountId) });

        var conversations = await db.Conversations.Where(c => c.PairLow == id || c.PairHigh == id).OrderBy(c => c.CreatedAt).ToListAsync(cancellationToken);
        var conversationIds = conversations.Select(c => c.Id).ToList();
        var messages = (await db.Messages.Where(m => conversationIds.Contains(m.ConversationId)).OrderBy(m => m.SentAt).ThenBy(m => m.Kind == MessageKind.System).ToListAsync(cancellationToken))
            .ToLookup(m => m.ConversationId);
        // #41: photo messages appear as they do in the chat: the member's own with their state, the other person's once passed.
        var mediaIds = messages.SelectMany(g => g).Where(m => m.MediaId is not null).Select(m => m.MediaId!.Value).ToList();
        var media = await db.MediaItems.Where(i => mediaIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        var chats = conversations.Select(c => new
        {
            with = Name(c.PairLow == id ? c.PairHigh : c.PairLow), c.CreatedAt, c.ClosedAt,
            messages = messages[c.Id].Where(m => AttachmentPolicy.Listed(m, id, m.MediaId is { } x && media.TryGetValue(x, out var i) ? i.Status : null,
                    m.MediaId is { } y && media.TryGetValue(y, out var j) && j.RemovedAt is not null))
                .Select(m => new
                {
                    from = m.Kind == MessageKind.System ? "system" : m.SenderAccountId == id ? "you" : "them", kind = m.ModerationRemovedAt!=null?"removed-by-moderation":m.Kind.ToString().ToLowerInvariant(), text = m.Kind == MessageKind.System ? ConversationEnding.SystemText(m.SenderAccountId == id) : MessagingPolicy.VisibleText(m), m.SentAt,
                    removedByModeration = m.ModerationRemovedAt is not null, deleted = m.DeletedAt is not null, photo = m.DeletedAt is null ? m.MediaId : null,
                    photoState = m.SenderAccountId == id && m.DeletedAt is null && m.MediaId is { } p && media.TryGetValue(p, out var item) ? MediaPolicy.MemberState(item.Status) : null,
                }),
        });

        var reports = await db.Reports.Where(r => r.ReporterAccountId == id).OrderBy(r => r.CreatedAt)
            .Select(r => new { category = r.CategoryId, subject = r.SubjectType, r.Text, r.CreatedAt }).ToListAsync(cancellationToken);
        var notices = await db.MemberNotices.Where(n => n.AccountId == id).OrderBy(n => n.CreatedAt).Select(n => new { n.Kind, n.CreatedAt, n.ReadAt }).ToListAsync(cancellationToken);
        var sanctions = await db.Sanctions.Where(s => s.AccountId == id).OrderBy(s => s.StartsAt)
            .Select(s => new { s.Kind, s.Reason, s.StartsAt, s.EndsAt, s.LiftedAt }).ToListAsync(cancellationToken);
        var appeals = await db.Appeals.Where(a => a.AccountId == id).OrderBy(a => a.CreatedAt).Select(a => new { a.Text, a.Status, a.CreatedAt, a.DecidedAt }).ToListAsync(cancellationToken);
        var verification = await db.IdVerificationAttempts.Where(a => a.AccountId == id).OrderBy(a => a.CreatedAt)
            .Select(a => new { a.State, a.CreatedAt }).ToListAsync(cancellationToken);
        var entitlements = await db.Entitlements.Where(e => e.AccountId == id).OrderBy(e => e.StartsAt)
            .Select(e => new { e.Tier, e.Source, e.StartsAt, e.EndsAt, e.RevokedAt }).ToListAsync(cancellationToken);

        return new
        {
            format = "swoovr-data-export/1",
            exportedAt = now,
            account = new { email = account.Email, createdAt = account.CreatedAt, status = account.Status, deletionDueAt = account.DeletionDueAt },
            profile = profile is null ? null : new
            {
                profile.DisplayName, profile.Area, profile.About, homeCity = home is null ? null : $"{home.Name}, {home.CountryName}",
                profile.DateOfBirth, profile.DateOfBirthVerified, profile.AgeStatus, profile.IdStatus, profile.IdCheckedAt, profile.PhotoStatus,
                profile.FoundingMember, profile.FounderPlace, profile.FoundingSince,
            },
            identity = identity is null ? null : new
            {
                gender = identity.GenderId, identity.GenderDescription, identity.Pronouns, orientation = identity.OrientationId,
                identity.ShowGender, identity.ShowOrientation, identity.ShowAge, identity.DatingShowMe,
            },
            modes, filters, trips, requests, blocks, conversations = chats, reportsYouMade = reports, notices, sanctions, appeals, meetingSafety,
            reportingCountryDeclarations = await db.Set<ModerationTransparencyFact>().Where(f=>f.SubjectAccountId==id&&f.Action=="termination")
                .Select(f=>new{f.ActionAt,f.Action,f.HomeCountryAtAction,f.CountryProvenance}).ToListAsync(cancellationToken),
            idVerification = verification, entitlements,
            // D203: the member's photos with their images (approved, checking or in review), as stored (metadata stripped).
            photos = photos ?? [],
            // #41: the photos the member sent in chat, with the image for approved ones (photos others sent are listed in the chats only).
            chatPhotos = chatPhotos ?? [],
            // D191: the member's own verified identity record, as kept (Q78: the export includes it).
            verifiedIdentity = verifiedIdentity is null ? null : new
            {
                verifiedIdentity.FirstName, verifiedIdentity.LastName, verifiedIdentity.DateOfBirth, verifiedIdentity.DocumentType,
                verifiedIdentity.IssuingCountry, verifiedIdentity.ExpiryDate, verifiedIdentity.Method,
            },
        };
    }

    /// <summary>Display names of everyone the member has a request, block or conversation with.</summary>
    private static async Task<Dictionary<Guid, string?>> NamesAsync(SwoovrDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var others = await db.ConnectionRequests.Where(r => r.FromAccountId == id || r.ToAccountId == id).Select(r => r.FromAccountId == id ? r.ToAccountId : r.FromAccountId)
            .Union(db.Conversations.Where(c => c.PairLow == id || c.PairHigh == id).Select(c => c.PairLow == id ? c.PairHigh : c.PairLow))
            .Union(db.Blocks.Where(b => b.BlockerAccountId == id).Select(b => b.BlockedAccountId))
            .ToListAsync(cancellationToken);
        return await db.MemberProfiles.Where(p => others.Contains(p.AccountId)).ToDictionaryAsync(p => p.AccountId, p => p.DisplayName, cancellationToken);
    }
}
