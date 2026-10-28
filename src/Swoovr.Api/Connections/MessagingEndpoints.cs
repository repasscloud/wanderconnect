using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Auth;
using Swoovr.Api.Profiles;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Connections;

/// <summary>
/// Messaging (Q42, D143, D165, D167): one conversation per connected pair across modes, text only, sent through these
/// routes and pushed to both members through the SignalR hub. Every send is checked on the server: an accepted connection, no
/// block either way, both accounts able to take part, bots only replying, travellers before arrival only replying.
/// Delete for both within five minutes; hide a message or a conversation for oneself. Moderators have no route to
/// conversations: only report evidence reaches a case. The audit holds ids only, never message text.
/// </summary>
public static class MessagingEndpoints
{
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization();

        api.MapGet("/conversations", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me) = (accounts.Db!, current!.Account!.Id);
            var rows = await (from c in db.Conversations
                              where (c.PairLow == me || c.PairHigh == me)
                                    && (c.ClosedAt == null || c.ClosedReason == ClosedReasons.Disconnected || c.ClosedReason == ClosedReasons.Deleted)
                                    && !db.Blocks.Any(b => (b.BlockerAccountId == c.PairLow && b.BlockedAccountId == c.PairHigh) || (b.BlockerAccountId == c.PairHigh && b.BlockedAccountId == c.PairLow))
                              let hidden = db.ConversationHides.Where(h => h.ConversationId == c.Id && h.AccountId == me).Select(h => (DateTimeOffset?)h.HiddenAt).FirstOrDefault()
                              where hidden == null || (c.LastMessageAt != null && c.LastMessageAt > hidden)
                              orderby c.LastMessageAt ?? c.CreatedAt descending
                              select c).Take(100).ToListAsync(cancellationToken);
            var views = new List<ConversationView>();
            foreach (var c in rows) views.Add(await ViewAsync(db, c, me, cancellationToken));
            return Results.Ok(views);
        });

        // Opens the conversation with a connected member (or returns the open one); anything else is "not available".
        api.MapPost("/conversations", async (StartConversation request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me, now) = (accounts.Db!, current!.Account!.Id, clock.GetUtcNow());
            var other = await db.MemberProfiles.SingleOrDefaultAsync(p => p.PublicId == request.To, cancellationToken);
            if (other is null || other.AccountId == me || !await ConnectedAsync(db, me, other.AccountId, cancellationToken))
                return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "You can message people you're connected with.");
            var (low, high) = ConnectionRequest.Pair(me, other.AccountId);
            var conversation = await db.Conversations.SingleOrDefaultAsync(c => c.PairLow == low && c.PairHigh == high && c.ClosedAt == null, cancellationToken);
            var created = conversation is null;
            if (created)
            {
                db.Conversations.Add(conversation = new Conversation { Id = Guid.NewGuid(), PairLow = low, PairHigh = high, CreatedAt = now });
                Audit.Record(db, now, me, "account", "conversation.started", "conversation", conversation.Id.ToString());
                try { await db.SaveChangesAsync(cancellationToken); }
                catch (DbUpdateException)
                {
                    // Both started it at once: the unique index keeps one open conversation per pair.
                    db.ChangeTracker.Clear();
                    conversation = await db.Conversations.SingleAsync(c => c.PairLow == low && c.PairHigh == high && c.ClosedAt == null, cancellationToken);
                    created = false;
                }
            }
            var view = await ViewAsync(db, conversation!, me, cancellationToken);
            return created ? Results.Created($"/api/v1/conversations/{conversation!.Id}", view) : Results.Ok(view);
        });

        api.MapGet("/conversations/{id:guid}", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me) = (accounts.Db!, current!.Account!.Id);
            var conversation = await ReadableAsync(db, id, me, cancellationToken);
            return conversation is null ? NotAvailable() : Results.Ok(await ViewAsync(db, conversation, me, cancellationToken));
        });

        api.MapGet("/conversations/{id:guid}/messages", async (Guid id, DateTimeOffset? before, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me, now) = (accounts.Db!, current!.Account!.Id, clock.GetUtcNow());
            var conversation = await ReadableAsync(db, id, me, cancellationToken);
            if (conversation is null) return NotAvailable();
            var hiddenAt = await db.ConversationHides.Where(h => h.ConversationId == id && h.AccountId == me).Select(h => (DateTimeOffset?)h.HiddenAt).FirstOrDefaultAsync(cancellationToken);
            // Q59, D167: messages deleted for both stay in the list as deleted, without their text.
            // #41: the recipient never sees a photo message before its photo passed screening (AttachmentPolicy.Listed).
            var rows = await db.Messages.Where(m => m.ConversationId == id && (before == null || m.SentAt < before)
                    && (hiddenAt == null || m.SentAt > hiddenAt) && !db.MessageHides.Any(h => h.MessageId == m.Id && h.AccountId == me)
                    && (m.Kind != MessageKind.Photo || m.SenderAccountId == me || m.DeletedAt != null
                        || db.MediaItems.Any(i => i.Id == m.MediaId && i.Status == MediaStatus.Passed && i.RemovedAt == null)))
                .OrderByDescending(m => m.SentAt).ThenByDescending(m => m.Id).Take(MessagingPolicy.PageSize + 1).ToListAsync(cancellationToken);
            var page = rows.Take(MessagingPolicy.PageSize).ToList();
            // #49: reading the conversation reads its message notifications (the member's own state; no read receipts).
            if (before is null)
                await db.Notifications.Where(n => n.AccountId == me && n.Kind == NotificationKind.Message && n.SubjectId == id && n.ReadAt == null)
                    .ExecuteUpdateAsync(u => u.SetProperty(n => n.ReadAt, now), cancellationToken);
            var media = await MediaForAsync(db, page, cancellationToken);
            var links = accounts.Services.GetRequiredService<Media.MediaLinks>();
            return Results.Ok(new MessagePage(page.Select(m => View(m, me, now, media.GetValueOrDefault(m.MediaId ?? Guid.Empty), links)).ToList(),
                rows.Count > MessagingPolicy.PageSize, page.LastOrDefault()?.SentAt));
        });

        api.MapPost("/conversations/{id:guid}/messages", async (Guid id, NewMessage request, ClaimsPrincipal user, AccountResolver accounts, IHubContext<MessagesHub> hub,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me, now) = (accounts.Db!, current!.Account!.Id, clock.GetUtcNow());
            var conversation = await OpenAsync(db, id, me, cancellationToken);
            if (conversation is null) return NotAvailable();
            var other = conversation.PairLow == me ? conversation.PairHigh : conversation.PairLow;
            var facts = await SendFactsAsync(db, current.Profile!.Bot, me, other, id, now, cancellationToken);
            switch (MessagingPolicy.CanSend(facts, request.Text))
            {
                case SendRefusal.NotAvailable: return NotAvailable();
                case SendRefusal.BotMayOnlyReply: return Problem(StatusCodes.Status403Forbidden, ProblemCodes.BotCannotInitiate, "Official bots reply only in conversations members start.");
                case SendRefusal.ArrivalRequired: return Problem(StatusCodes.Status403Forbidden, ProblemCodes.ArrivalRequired, "You can reply, or write first once you've confirmed you've arrived.");
                case SendRefusal.InvalidText: return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidMessage, "Write 1 to 2,000 characters.");
                case SendRefusal.RateLimited: return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.TooManyRequests, "That's a lot of messages. Slow down a little.");
            }
            var message = new Message { Id = Guid.NewGuid(), ConversationId = id, SenderAccountId = me, Kind = MessageKind.Text, Body = MessagingPolicy.CleanText(request.Text)!, SentAt = now };
            db.Messages.Add(message);
            conversation.LastMessageAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await hub.Clients.Group(MessagesHub.Group(me)).SendAsync("message", View(message, me, now), cancellationToken);
            await hub.Clients.Group(MessagesHub.Group(other)).SendAsync("message", View(message, other, now), cancellationToken);
            await accounts.Services.GetRequiredService<Notifications.Notifier>().NotifyAsync(other, NotificationKind.Message, me, null, id, now, cancellationToken, message.Body);
            return Results.Created($"/api/v1/messages/{message.Id}", View(message, me, now));
        });

        // Rule 8: delete for both, by the sender, within five minutes.
        api.MapDelete("/messages/{id:guid}", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, IHubContext<MessagesHub> hub, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me, now) = (accounts.Db!, current!.Account!.Id, clock.GetUtcNow());
            var message = await db.Messages.FindAsync([id], cancellationToken);
            var conversation = message is null ? null : await OpenAsync(db, message.ConversationId, me, cancellationToken);
            if (message is null || conversation is null || message.SenderAccountId != me || message.DeletedAt is not null) return NotAvailable();
            if (!MessagingPolicy.MayDeleteForBoth(message, me, now))
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.DeleteWindowPassed, "Messages can be deleted for both of you within five minutes.");
            message.DeletedAt = now;
            Audit.Record(db, now, me, "account", "message.deleted", "message", message.Id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            var deleted = new MessageDeleted(conversation.Id, message.Id);
            await hub.Clients.Groups(MessagesHub.Group(conversation.PairLow), MessagesHub.Group(conversation.PairHigh)).SendAsync("deleted", deleted, cancellationToken);
            return Results.NoContent();
        });

        // Rule 9: hide for oneself only.
        api.MapPost("/messages/{id:guid}/hide", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me, now) = (accounts.Db!, current!.Account!.Id, clock.GetUtcNow());
            var message = await db.Messages.FindAsync([id], cancellationToken);
            if (message is null || await OpenAsync(db, message.ConversationId, me, cancellationToken) is null) return NotAvailable();
            if (await db.MessageHides.FindAsync([id, me], cancellationToken) is null) db.MessageHides.Add(new MessageHide { MessageId = id, AccountId = me, HiddenAt = now });
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        api.MapPost("/conversations/{id:guid}/hide", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me, now) = (accounts.Db!, current!.Account!.Id, clock.GetUtcNow());
            if (await OpenAsync(db, id, me, cancellationToken) is null) return NotAvailable();
            var hide = await db.ConversationHides.FindAsync([id, me], cancellationToken);
            if (hide is null) db.ConversationHides.Add(new ConversationHide { ConversationId = id, AccountId = me, HiddenAt = now });
            else hide.HiddenAt = now;
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        // #41 (policy §5, D203): a photo in chat, through the same quarantine as profile photos. Same send rules as text,
        // the recipient's "photos in chat" choice, 20 a day. The sender sees it at once with its state; the recipient only
        // once it passed (AttachmentChangedAsync).
        api.MapPost("/conversations/{id:guid}/photos", async (Guid id, HttpRequest request, ClaimsPrincipal user, AccountResolver accounts, IHubContext<MessagesHub> hub,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me, now) = (accounts.Db!, current!.Account!.Id, clock.GetUtcNow());
            var conversation = await OpenAsync(db, id, me, cancellationToken);
            if (conversation is null) return NotAvailable();
            var other = conversation.PairLow == me ? conversation.PairHigh : conversation.PairLow;
            switch (MessagingPolicy.CanSend(await SendFactsAsync(db, current.Profile!.Bot, me, other, id, now, cancellationToken), "photo"))
            {
                case SendRefusal.NotAvailable: return NotAvailable();
                case SendRefusal.BotMayOnlyReply: return Problem(StatusCodes.Status403Forbidden, ProblemCodes.BotCannotInitiate, "Official bots reply only in conversations members start.");
                case SendRefusal.ArrivalRequired: return Problem(StatusCodes.Status403Forbidden, ProblemCodes.ArrivalRequired, "You can reply, or write first once you've confirmed you've arrived.");
                case SendRefusal.RateLimited: return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.TooManyRequests, "That's a lot of messages. Slow down a little.");
            }
            if (!(await Notifications.Notifier.SettingsAsync(db, other, cancellationToken)).ChatPhotos)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.PhotosOff, "This person isn't accepting photos in chat.");
            var pipeline = accounts.Services.GetRequiredService<Media.MediaPipeline>();
            if (!pipeline.Available) return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.MediaUnavailable, "Photos aren't available right now.");
            if (await db.MediaItems.CountAsync(i => i.AccountId == me && i.Purpose == AttachmentPolicy.Purpose && i.CreatedAt > now - AttachmentPolicy.Day, cancellationToken) >= AttachmentPolicy.PerDay)
                return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.AttachmentLimit, $"You can send up to {AttachmentPolicy.PerDay} photos a day.");
            if (!request.HasFormContentType) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhotoInvalid, "Send the photo as a file.");
            if (request.ContentLength > MediaPolicy.MaxUploadBytes + 64 * 1024) return Problem(StatusCodes.Status413PayloadTooLarge, ProblemCodes.PhotoTooBig, "Photos can be up to 10 MB.");
            IFormCollection form;
            try { form = await request.ReadFormAsync(cancellationToken); }
            catch (Exception exception) when (exception is InvalidDataException or IOException or BadHttpRequestException)
            { return Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhotoInvalid, "That upload couldn't be read."); }
            if (form.Files.GetFile("file") is not { } file) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhotoInvalid, "Choose a photo.");
            if (file.Length > MediaPolicy.MaxUploadBytes) return Problem(StatusCodes.Status413PayloadTooLarge, ProblemCodes.PhotoTooBig, "Photos can be up to 10 MB.");
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);
            var (item, error) = await pipeline.UploadAsync(me, buffer.ToArray(), null, now, cancellationToken, conversation.Id);
            if (error != MediaUploadError.None) return error switch
            {
                MediaUploadError.TooBig => Problem(StatusCodes.Status413PayloadTooLarge, ProblemCodes.PhotoTooBig, "Photos can be up to 10 MB."),
                MediaUploadError.UnsupportedType or MediaUploadError.Animated => Problem(StatusCodes.Status415UnsupportedMediaType, ProblemCodes.PhotoUnsupported, "Use a JPEG, PNG or WebP photo."),
                MediaUploadError.TooSmall => Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhotoInvalid, $"Photos need to be at least {MediaPolicy.MinSide} pixels on each side."),
                _ => Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhotoInvalid, "That photo couldn't be read."),
            };
            // The message exists from now, for the sender only; LastMessageAt moves when the recipient can see it.
            var message = new Message { Id = Guid.NewGuid(), ConversationId = id, SenderAccountId = me, Kind = MessageKind.Photo, Body = "", SentAt = now, MediaId = item!.Id };
            db.Messages.Add(message);
            await db.SaveChangesAsync(cancellationToken);
            var view = View(message, me, now, item);
            await hub.Clients.Group(MessagesHub.Group(me)).SendAsync("message", view, cancellationToken);
            return Results.Accepted($"/api/v1/messages/{message.Id}", view);
        }).DisableAntiforgery();

        // #41, Q85: end politely without blocking. An optional fixed closing message, then every mode connecting the pair is
        // disconnected (D176: the other may not ask again in those modes for 30 days) and the chat closes read-only for both.
        api.MapPost("/conversations/{id:guid}/end", async (Guid id, EndConversation request, ClaimsPrincipal user, AccountResolver accounts, IHubContext<MessagesHub> hub,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me, now) = (accounts.Db!, current!.Account!.Id, clock.GetUtcNow());
            if (!ConversationEnding.Valid(request.Closing)) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidClosing, "Choose one of the closing messages, or none.");
            var conversation = await OpenAsync(db, id, me, cancellationToken);
            if (conversation is null) return NotAvailable();
            var other = conversation.PairLow == me ? conversation.PairHigh : conversation.PairLow;
            Message? closing = null;
            if (request.Closing is { } choice && MessagingPolicy.CanSend(await SendFactsAsync(db, current.Profile!.Bot, me, other, id, now, cancellationToken) with { SentLastMinute = 0 },
                    ConversationEnding.Closings[choice]) == SendRefusal.None)
            {
                db.Messages.Add(closing = new Message { Id = Guid.NewGuid(), ConversationId = id, SenderAccountId = me, Kind = MessageKind.Text, Body = ConversationEnding.Closings[choice], SentAt = now });
                conversation.LastMessageAt = now;
            }
            foreach (var accepted in await db.ConnectionRequests.Where(r => r.PairLow == conversation.PairLow && r.PairHigh == conversation.PairHigh
                         && r.Status == ConnectionRequestStatus.Accepted).ToListAsync(cancellationToken))
                if (ConnectionRequestPolicy.Disconnect(accepted, me, now))
                    Audit.Record(db, now, me, "account", "request.disconnected", "connection-request", accepted.Id.ToString(), accepted.ModeId);
            Audit.Record(db, now, me, "account", "conversation.ended", "conversation", id.ToString(), request.Closing ?? "none");
            await db.SaveChangesAsync(cancellationToken);
            var closed = await CloseIfUnconnectedAsync(db, conversation.PairLow, conversation.PairHigh, now, cancellationToken);
            if (closed is not null) await db.SaveChangesAsync(cancellationToken);
            if (closing is not null)
            {
                await hub.Clients.Group(MessagesHub.Group(me)).SendAsync("message", View(closing, me, now), cancellationToken);
                await hub.Clients.Group(MessagesHub.Group(other)).SendAsync("message", View(closing, other, now), cancellationToken);
                await accounts.Services.GetRequiredService<Notifications.Notifier>().NotifyAsync(other, NotificationKind.Message, me, null, id, now, cancellationToken, closing.Body);
            }
            if (closed is not null) await PushClosedAsync(hub, [closed], cancellationToken);
            return Results.Ok(await ViewAsync(db, conversation, me, cancellationToken));
        });

        // #41: up to three starters from intention chips both picked in a mode they're connected in (never another mode, D175).
        api.MapGet("/conversations/{id:guid}/starters", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me) = (accounts.Db!, current!.Account!.Id);
            var conversation = await OpenAsync(db, id, me, cancellationToken);
            if (conversation is null) return NotAvailable();
            var other = conversation.PairLow == me ? conversation.PairHigh : conversation.PairLow;
            var modes = await db.ConnectionRequests.Where(r => r.PairLow == conversation.PairLow && r.PairHigh == conversation.PairHigh && r.Status == ConnectionRequestStatus.Accepted)
                .Select(r => r.ModeId).Distinct().ToListAsync(cancellationToken);
            var shared = await (from mine in db.MemberModeIntentions
                                join theirs in db.MemberModeIntentions on new { mine.ModeId, mine.IntentionId } equals new { theirs.ModeId, theirs.IntentionId }
                                join chip in db.ModeIntentions on mine.IntentionId equals chip.Id
                                where mine.AccountId == me && theirs.AccountId == other && modes.Contains(mine.ModeId) && chip.Available
                                orderby chip.ModeId, chip.SortOrder select chip.Label).ToListAsync(cancellationToken);
            var texts = ConversationStarters.For(shared);
            return Results.Ok(texts.Select((text, i) => new ConversationStarter(shared.Distinct().ElementAt(i), text)).ToList());
        });

        app.MapHub<MessagesHub>("/hubs/messages").RequireAuthorization();
    }

    private static async Task<SendFacts> SendFactsAsync(SwoovrDbContext db, bool senderIsBot, Guid me, Guid other, Guid conversation, DateTimeOffset now,
        CancellationToken cancellationToken) =>
        new(senderIsBot, await ConnectedAsync(db, me, other, cancellationToken), await ParticipatesAsync(db, other, now, cancellationToken),
            await db.Messages.AnyAsync(m => m.ConversationId == conversation && m.SenderAccountId == other, cancellationToken),
            await TravellerBeforeArrivalAsync(db, me, other, now, cancellationToken),
            await db.Messages.CountAsync(m => m.SenderAccountId == me && m.SentAt > now.AddMinutes(-1), cancellationToken));

    /// <summary>
    /// #41: a chat photo's screening moved on. The sender's chat gets the new state; once it passed the conversation moves
    /// up for both, the recipient's chat shows it and they're notified (unless a block or the conversation closing came
    /// first). Uncertain, rejected and blocked photos reach only the sender, as "in review" or "not approved".
    /// </summary>
    public static async Task AttachmentChangedAsync(IServiceProvider services, MediaItem item, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<SwoovrDbContext>();
        var message = await db.Messages.SingleOrDefaultAsync(m => m.MediaId == item.Id, cancellationToken);
        if (message is null) return;
        var hub = services.GetRequiredService<IHubContext<MessagesHub>>();
        var links = services.GetRequiredService<Media.MediaLinks>();
        await hub.Clients.Group(MessagesHub.Group(message.SenderAccountId)).SendAsync("message", View(message, message.SenderAccountId, now, item, links), cancellationToken);
        if (item.Status != MediaStatus.Passed || message.DeletedAt is not null) return;
        var conversation = await OpenAsync(db, message.ConversationId, message.SenderAccountId, cancellationToken);
        if (conversation is null) return;
        var other = conversation.PairLow == message.SenderAccountId ? conversation.PairHigh : conversation.PairLow;
        conversation.LastMessageAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await hub.Clients.Group(MessagesHub.Group(other)).SendAsync("message", View(message, other, now, item, links), cancellationToken);
        await services.GetRequiredService<Notifications.Notifier>().NotifyAsync(other, NotificationKind.Message, message.SenderAccountId, null, conversation.Id, now, cancellationToken);
    }

    private static IResult NotAvailable() => Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "That conversation isn't available.");

    /// <summary>
    /// A message for one viewer. A photo message carries a short-lived link once it passed (never after delete for both),
    /// and its state for the sender only (a blocked photo reads as not approved, procedure §4).
    /// </summary>
    public static MessageView View(Message message, Guid viewer, DateTimeOffset now, MediaItem? media = null, Media.MediaLinks? links = null)
    {
        PhotoView? photo = null;
        string? state = null;
        if (message.Kind == MessageKind.Photo && message.DeletedAt is null && media is not null)
        {
            if (media.Status == MediaStatus.Passed && media.RemovedAt is null && links?.For(media.Id, "member", now) is { } link) photo = new PhotoView(media.Id, link);
            if (message.SenderAccountId == viewer) state = media.RemovedAt is not null ? "removed" : MediaPolicy.MemberState(media.Status);
        }
        return new(message.Id, message.ConversationId, message.SenderAccountId == viewer, Wire(message.Kind), MessagingPolicy.VisibleText(message), message.SentAt,
            MessagingPolicy.MayDeleteForBoth(message, viewer, now), message.DeletedAt is not null, photo, state);
    }

    private static async Task<Dictionary<Guid, MediaItem>> MediaForAsync(SwoovrDbContext db, IEnumerable<Message> messages, CancellationToken cancellationToken)
    {
        var ids = messages.Where(m => m.MediaId is not null).Select(m => m.MediaId!.Value).ToList();
        return ids.Count == 0 ? [] : await db.MediaItems.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
    }

    /// <summary>The member's open conversation with no block either way (rules 12–13); anything else is null, read as not available.</summary>
    public static async Task<Conversation?> OpenAsync(SwoovrDbContext db, Guid id, Guid me, CancellationToken cancellationToken) =>
        await db.Conversations.SingleOrDefaultAsync(c => c.Id == id && (c.PairLow == me || c.PairHigh == me) && c.ClosedAt == null
            && !db.Blocks.Any(b => (b.BlockerAccountId == c.PairLow && b.BlockedAccountId == c.PairHigh) || (b.BlockerAccountId == c.PairHigh && b.BlockedAccountId == c.PairLow)), cancellationToken);

    /// <summary>
    /// What the member may read: an open conversation, or the read-only history of one closed because no mode connects
    /// the pair any more (D176) or the other account was deleted (D170; their messages are gone, the member keeps their
    /// own). A block hides it for good (D143). Nothing can be sent to a closed conversation.
    /// </summary>
    public static async Task<Conversation?> ReadableAsync(SwoovrDbContext db, Guid id, Guid me, CancellationToken cancellationToken) =>
        await OpenAsync(db, id, me, cancellationToken)
        ?? await db.Conversations.SingleOrDefaultAsync(c => c.Id == id && (c.PairLow == me || c.PairHigh == me) && c.ClosedAt != null
            && (c.ClosedReason == ClosedReasons.Disconnected || c.ClosedReason == ClosedReasons.Deleted)
            && !db.Blocks.Any(b => (b.BlockerAccountId == c.PairLow && b.BlockedAccountId == c.PairHigh) || (b.BlockerAccountId == c.PairHigh && b.BlockedAccountId == c.PairLow)), cancellationToken);

    /// <summary>Rule 4: an accepted connection in any mode, and no block either way.</summary>
    public static async Task<bool> ConnectedAsync(SwoovrDbContext db, Guid me, Guid other, CancellationToken cancellationToken)
    {
        var (low, high) = ConnectionRequest.Pair(me, other);
        return await db.ConnectionRequests.AnyAsync(r => r.PairLow == low && r.PairHigh == high && r.Status == ConnectionRequestStatus.Accepted, cancellationToken)
            && !await db.Blocks.AnyAsync(b => (b.BlockerAccountId == me && b.BlockedAccountId == other) || (b.BlockerAccountId == other && b.BlockedAccountId == me), cancellationToken);
    }

    /// <summary>Rule 15, D160: the other member's account is active and not restricted, suspended or terminated.</summary>
    private static async Task<bool> ParticipatesAsync(SwoovrDbContext db, Guid other, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.Accounts.AnyAsync(a => a.Id == other && a.Status == AccountStatus.Active, cancellationToken)
        && !await db.Sanctions.AnyAsync(s => s.AccountId == other && s.LiftedAt == null && s.StartsAt <= now && (s.EndsAt == null || s.EndsAt > now) && s.Kind != SanctionKind.Warning, cancellationToken);

    /// <summary>
    /// D60, D149: true while the sender's only connections with the other member came from the sender's own visit and
    /// that trip is still upcoming (not arrived, not over). Then they may reply but not write first.
    /// </summary>
    private static async Task<bool> TravellerBeforeArrivalAsync(SwoovrDbContext db, Guid me, Guid other, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var (low, high) = ConnectionRequest.Pair(me, other);
        var accepted = await db.ConnectionRequests.Where(r => r.PairLow == low && r.PairHigh == high && r.Status == ConnectionRequestStatus.Accepted).ToListAsync(cancellationToken);
        if (accepted.Count == 0 || accepted.Any(r => r.TripId is null)) return false;
        var trips = await (from t in db.Trips join c in db.GeoCities on t.GeoNameId equals c.GeoNameId
                           where accepted.Select(r => r.TripId).Contains(t.Id) select new { t, c }).ToListAsync(cancellationToken);
        if (trips.Count == 0 || trips.Any(x => x.t.AccountId != me)) return false;
        return trips.Any(x => x.t.ArrivedAt is null && TravelPolicy.State(new TripFacts(x.t, x.c.TimeZone), [], now) is not (TripState.Cancelled or TripState.Expired));
    }

    private static async Task<ConversationView> ViewAsync(SwoovrDbContext db, Conversation conversation, Guid me, CancellationToken cancellationToken)
    {
        var otherId = conversation.PairLow == me ? conversation.PairHigh : conversation.PairLow;
        var other = await db.MemberProfiles.SingleOrDefaultAsync(p => p.AccountId == otherId, cancellationToken);
        // D170: after final deletion the other member has no profile; the conversation reads "Deleted member".
        if (other is null) return new(conversation.Id, new PersonSummary(Guid.Empty, "Deleted member", []), conversation.CreatedAt, conversation.LastMessageAt, false);
        var person = new PersonSummary(other.PublicId, other.DisplayName, ProfileEndpoints.Badges(other, await ProfileEndpoints.BadgeOrderAsync(db, cancellationToken)));
        var canSend = conversation.ClosedAt is null && await ConnectedAsync(db, me, otherId, cancellationToken)
            && await ParticipatesAsync(db, otherId, DateTimeOffset.UtcNow, cancellationToken);
        return new(conversation.Id, person, conversation.CreatedAt, conversation.LastMessageAt, canSend);
    }

    /// <summary>
    /// Q42 rules 12–14: a block ends the pair's accepted connections and closes their open conversation at once, and
    /// tells both members' clients. Unblocking restores neither; a new connection starts a new conversation.
    /// </summary>
    public static async Task<Conversation?> CloseForBlockAsync(SwoovrDbContext db, Guid me, Guid other, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var (low, high) = ConnectionRequest.Pair(me, other);
        foreach (var accepted in await db.ConnectionRequests.Where(r => r.PairLow == low && r.PairHigh == high && r.Status == ConnectionRequestStatus.Accepted).ToListAsync(cancellationToken))
        {
            accepted.Status = ConnectionRequestStatus.Ended;
            Audit.Record(db, now, null, "system", "request.ended", "connection-request", accepted.Id.ToString(), "block");
        }
        var open = await db.Conversations.SingleOrDefaultAsync(c => c.PairLow == low && c.PairHigh == high && c.ClosedAt == null, cancellationToken);
        if (open is not null)
        {
            (open.ClosedAt, open.ClosedReason) = (now, ClosedReasons.Blocked);
            Audit.Record(db, now, null, "system", "conversation.closed", "conversation", open.Id.ToString(), "block");
        }
        return open;
    }

    /// <summary>#82: tells both members' open chat windows that these conversations closed (after the change is saved).</summary>
    public static async Task PushClosedAsync(IHubContext<MessagesHub> hub, IEnumerable<Conversation> closed, CancellationToken cancellationToken)
    {
        foreach (var conversation in closed)
            await hub.Clients.Groups(MessagesHub.Group(conversation.PairLow), MessagesHub.Group(conversation.PairHigh))
                .SendAsync("closed", new ConversationClosed(conversation.Id), cancellationToken);
    }

    /// <summary>
    /// D176: after a disconnect, the open conversation closes read-only when no accepted connection in any mode is left;
    /// it stays open while another mode still connects the pair. Returns the closed conversation, if any, for the push.
    /// </summary>
    public static async Task<Conversation?> CloseIfUnconnectedAsync(SwoovrDbContext db, Guid low, Guid high, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await db.ConnectionRequests.AnyAsync(r => r.PairLow == low && r.PairHigh == high && r.Status == ConnectionRequestStatus.Accepted, cancellationToken)) return null;
        var open = await db.Conversations.SingleOrDefaultAsync(c => c.PairLow == low && c.PairHigh == high && c.ClosedAt == null, cancellationToken);
        if (open is null) return null;
        (open.ClosedAt, open.ClosedReason) = (now, ClosedReasons.Disconnected);
        Audit.Record(db, now, null, "system", "conversation.closed", "conversation", open.Id.ToString(), "disconnect");
        return open;
    }
}

/// <summary>
/// The messaging hub (D143): members only, server-to-client pushes ("message", "deleted", "closed") to each member's own
/// group. Clients send through the REST routes, so every send passes the same server-side checks.
/// </summary>
[Authorize]
public sealed class MessagesHub(AccountResolver accounts) : Hub
{
    public static string Group(Guid account) => "account:" + account;

    public override async Task OnConnectedAsync()
    {
        var (current, denied) = await MemberAsync(Context.User!, accounts, Context.ConnectionAborted);
        if (denied is not null || current?.Account is null) { Context.Abort(); return; }
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(current.Account.Id), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }
}
