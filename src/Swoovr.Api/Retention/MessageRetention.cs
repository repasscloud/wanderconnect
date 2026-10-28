using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Retention;

public sealed record MessageRetentionRun(int Texts, int Photos, int Kept, bool HoldStatusUnavailable = false);

public sealed class MessageRetention(SwoovrDbContext db, EvidencePreservation evidence)
{
    public async Task<MessageRetentionRun> PurgeAsync(DateTimeOffset now, CancellationToken token)
    {
        if (await evidence.TryLiveAsync(token) is null) return new(0, 0, 0, true);
        var (texts, photos, kept) = (0, 0, 0);
        var due = await db.Messages.AsNoTracking().Where(m => m.DeletedAt != null && m.DeletedAt < now - MessagingPolicy.EvidenceRetention
            && (m.Body != "" || m.Kind == MessageKind.Photo && m.MediaId != null)).Select(m => new { m.Id, m.SenderAccountId, m.MediaId }).ToListAsync(token);
        foreach (var row in due)
        {
            await using var state = await AccountLifecycleLease.AcquireAsync(db, row.SenderAccountId, token);
            var owner = row.MediaId is { } photo ? await db.MediaItems.Where(i => i.Id == photo).Select(i => (Guid?)i.AccountId).SingleOrDefaultAsync(token) : null;
            await using var media = owner is { } account ? await AccountLifecycleLease.AcquireMediaAsync(db, account, token) : null;
            await using var barrier = await PreservationLease.SharedAsync(db, token);
            var message = await db.Messages.SingleOrDefaultAsync(m => m.Id == row.Id, token);
            if (message is null) continue;
            await db.Entry(message).ReloadAsync(token);
            if (message.DeletedAt is null || message.DeletedAt >= now - MessagingPolicy.EvidenceRetention) continue;
            var conversation = await db.Conversations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == message.ConversationId, token);
            var live = await evidence.TryLiveAsync(token);
            if (live is null) return new(texts, photos, kept, true);
            if (conversation is not null && EvidencePreservation.MessageHeld(message, conversation, live.Value.Holds, live.Value.Scopes)) { kept++; continue; }
            if (MessagingPolicy.Purge(message, now)) { message.Body = ""; texts++; }
            if (message.Kind == MessageKind.Photo && message.MediaId is { } mediaId)
            {
                var item = await db.MediaItems.SingleOrDefaultAsync(i => i.Id == mediaId, token);
                if (item is not null)
                {
                    await db.Entry(item).ReloadAsync(token);
                    if (item.RemovedAt is null)
                    {
                        item.RemovedAt = now;
                        if (item.Status is MediaStatus.Pending or MediaStatus.Passed or MediaStatus.Uncertain) (item.Status, item.NextScreenAt) = (MediaStatus.Removed, null);
                        photos++;
                    }
                }
            }
            await db.SaveChangesAsync(token);
        }
        return new(texts, photos, kept);
    }
}
