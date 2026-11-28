using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
public static partial class MessagingEndpoints
{
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/conversations", async (ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me, now) = (accounts.Db!, current!.Account!.Id, clock.GetUtcNow());
            // Issue 541: latest activity first (creation time for an empty chat), then a fixed tie-break.
            var rows = await Listed(db, me).OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt).ThenByDescending(c => c.Id)
                .Take(100).ToListAsync(cancellationToken);
            var unread = await UnreadAsync(db, me, rows.Select(c => c.Id).ToList(), cancellationToken);
            var views = new List<ConversationView>();
            foreach (var c in rows) views.Add((await ViewAsync(db, c, me, cancellationToken)) with { Unread = unread.GetValueOrDefault(c.Id) });
            return Results.Ok(await SummarizeAsync(db, accounts.Services, me, rows, views, now, cancellationToken));
        });

        // Opens the conversation with a connected member (or returns the open one); anything else is "not available".
        api.MapPost("/conversations", async (StartConversation request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            // D325: a restriction that names messages stops the member writing; everything else stays open.
            if (current!.Enforcement.NoMessages) return Problem(StatusCodes.Status403Forbidden, ProblemCodes.Restricted, "You can't send messages right now.");
            var (db, me, now) = (accounts.Db!, current!.Account!.Id, clock.GetUtcNow());
            await using var lifecycle = await AccountLifecycleLease.AcquireAsync(db, me, cancellationToken);
            await using var barrier = await Retention.PreservationLease.SharedAsync(db, cancellationToken);
            if (!await db.Accounts.AsNoTracking().AnyAsync(a => a.Id == me && a.Status == AccountStatus.Active, cancellationToken)) return NotAvailable();
            var other = await db.MemberProfiles.SingleOrDefaultAsync(p => p.PublicId == request.To, cancellationToken);
            if (other is null || other.AccountId == me || !await ConnectedAsync(db, me, other.AccountId, cancellationToken))
                return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "You can message people you're connected with.");
            var (low, high) = ConnectionRequest.Pair(me, other.AccountId);
            var conversation = await db.Conversations.SingleOrDefaultAsync(c => c.PairLow == low && c.PairHigh == high && c.ClosedAt == null, cancellationToken);
            var created = conversation is null;
            if (created)
            {
                // Select only an accepted pair mode. Older clients omit it: earliest accepted connection is deterministic.
                var accepted = db.ConnectionRequests.Where(r => r.PairLow == low && r.PairHigh == high && r.Status == ConnectionRequestStatus.Accepted);
                var origin = request.Mode is null
                    ? await accepted.OrderBy(r => r.AnsweredAt).ThenBy(r => r.CreatedAt).ThenBy(r => r.Id).Select(r => r.ModeId).FirstOrDefaultAsync(cancellationToken)
                    : await accepted.Where(r => r.ModeId == request.Mode).Select(r => r.ModeId).FirstOrDefaultAsync(cancellationToken);
                if (origin is null) return NotAvailable();
                db.Conversations.Add(conversation = new Conversation { Id = Guid.NewGuid(), PairLow = low, PairHigh = high, CreatedAt = now, OriginModeId = origin });
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
            return created ? Results.Created($"/api/conversations/{conversation!.Id}", view) : Results.Ok(view);
        });

        api.MapGet("/conversations/{id:guid}", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me) = (accounts.Db!, current!.Account!.Id);
            var conversation = await ReadableAsync(db, id, me, cancellationToken);
            if (conversation is null) return NotAvailable();
            // Issue 542: the header shows the same summary as the Chats row (thumbnail, published age, mode).
            // Read before the messages are opened, so the thread can start at the first unread one.
            var view = (await ViewAsync(db, conversation, me, cancellationToken, unanswered: true))
                with { Unread = (await UnreadAsync(db, me, [conversation.Id], cancellationToken)).GetValueOrDefault(conversation.Id) };
            return Results.Ok((await SummarizeAsync(db, accounts.Services, me, [conversation], [view], clock.GetUtcNow(), cancellationToken))[0]);
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
                    // PR 578 review: their photo that passed after the hide is new to this member, as the unread count says.
                    && (hiddenAt == null || m.SentAt > hiddenAt || (m.Kind == MessageKind.Photo && m.SenderAccountId != me
                        && db.MediaItems.Any(i => i.Id == m.MediaId && (i.ReviewedAt ?? i.ScreenedAt) > hiddenAt)))
                    && !db.MessageHides.Any(h => h.MessageId == m.Id && h.AccountId == me)
                    && (m.Kind != MessageKind.Photo || m.SenderAccountId == me || m.ExplicitRefusedAt == null && (m.DeletedAt != null
                        || db.MediaItems.Any(i => i.Id == m.MediaId && i.Status == MediaStatus.Passed && i.RemovedAt == null))))
                .OrderByDescending(m => m.SentAt).ThenByDescending(m => m.Kind == MessageKind.System).ThenByDescending(m => m.Id).Take(MessagingPolicy.PageSize + 1).ToListAsync(cancellationToken);
            var page = rows.Take(MessagingPolicy.PageSize).ToList();
            Guid? firstUnread = null;
            // #49: reading the conversation reads its message notifications (the member's own state; no read receipts).
            if (before is null)
            {
                // PR 579 review: where the member's reading starts, found before this read moves their position.
                firstUnread = await UnreadMessages(db, me).Where(m => m.ConversationId == id)
                    .OrderBy(m => m.SentAt).ThenBy(m => m.Id).Select(m => (Guid?)m.Id).FirstOrDefaultAsync(cancellationToken);
                // Issue 541: this member has now read the conversation (their own unread count; the sender is never told).
                await MarkReadAsync(db, id, me, now, cancellationToken);
                await db.Notifications.Where(n => n.AccountId == me && (n.Kind == NotificationKind.Message || n.Kind == NotificationKind.ConversationEnded) && n.SubjectId == id && n.ReadAt == null)
                    .ExecuteUpdateAsync(u => u.SetProperty(n => n.ReadAt, now), cancellationToken);
            }
            var media = await MediaForAsync(db, page, cancellationToken);
            var links = accounts.Services.GetRequiredService<Media.MediaLinks>();
            // Issue 545: each meet-up card with its current state.
            var meetups = await MeetupsForAsync(db, conversation, page, me, now, cancellationToken);
            // D423 (182.4): their explicit photos load only while this member may receive them now, in an open conversation.
            var otherId = conversation.PairLow == me ? conversation.PairHigh : conversation.PairLow;
            var explicitAllowed = conversation.ClosedAt is null && media.Values.Any(i => i.AccountId != me && i.ContentRating == ContentRating.Explicit)
                && await ExplicitChat.AllowedAsync(db, otherId, me, cancellationToken);
            return Results.Ok(new MessagePage(page.Select(m => View(m, me, now, media.GetValueOrDefault(m.MediaId ?? Guid.Empty), links, meetups.GetValueOrDefault(m.Id), explicitAllowed)).ToList(),
                rows.Count > MessagingPolicy.PageSize, page.LastOrDefault()?.SentAt, firstUnread));
        });

        // Issue 541: a message that arrived live while the member had the conversation open is read too.
        api.MapPost("/conversations/{id:guid}/read", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me, now) = (accounts.Db!, current!.Account!.Id, clock.GetUtcNow());
            if (await ReadableAsync(db, id, me, cancellationToken) is null) return NotAvailable();
            await MarkReadAsync(db, id, me, now, cancellationToken);
            await db.Notifications.Where(n => n.AccountId == me && (n.Kind == NotificationKind.Message || n.Kind == NotificationKind.ConversationEnded) && n.SubjectId == id && n.ReadAt == null)
                .ExecuteUpdateAsync(u => u.SetProperty(n => n.ReadAt, now), cancellationToken);
            return Results.NoContent();
        });

        api.MapPost("/conversations/{id:guid}/messages", async (Guid id, NewMessage request, ClaimsPrincipal user, AccountResolver accounts, IHubContext<MessagesHub> hub,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            // D325: a restriction that names messages stops the member writing; everything else stays open.
            if (current!.Enforcement.NoMessages) return Problem(StatusCodes.Status403Forbidden, ProblemCodes.Restricted, "You can't send messages right now.");
            var (db, me, now) = (accounts.Db!, current!.Account!.Id, clock.GetUtcNow());
            await using var lifecycle = await AccountLifecycleLease.AcquireAsync(db, me, cancellationToken);
            if (!await db.Accounts.AsNoTracking().AnyAsync(a => a.Id == me && a.Status == AccountStatus.Active, cancellationToken)) return NotAvailable();
            await using var barrier = await Retention.PreservationLease.SharedAsync(db, cancellationToken);
            await using var transaction = await LockAsync(db, id, me, cancellationToken);
            now = clock.GetUtcNow();
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
                case SendRefusal.AwaitingReply: return AwaitingReply();
                case SendRefusal.RateLimited: return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.TooManyRequests, "That's a lot of messages. Slow down a little.");
            }
            var message = new Message { Id = Guid.NewGuid(), ConversationId = id, SenderAccountId = me, Kind = MessageKind.Text, Body = MessagingPolicy.CleanText(request.Text)!, SentAt = now };
            accounts.Services.GetRequiredService<Swoovr.Api.Moderation.TextHarmQueue>().Enqueue(me,TextHarmSource.Message,message.Id,null,message.Body,now);
            db.Messages.Add(message);
            conversation.LastMessageAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await barrier.DisposeAsync();
            await lifecycle.DisposeAsync();
            await MemberMutationAuthority.ReleaseForEffectsAsync(accounts.Services);
            await BetaMessageAudience.SendAsync(accounts.Services,me,View(message,me,now),cancellationToken);
            await BetaMessageAudience.SendAsync(accounts.Services,other,View(message,other,now),cancellationToken);
            await accounts.Services.GetRequiredService<Notifications.Notifier>().NotifyAsync(other, NotificationKind.Message, me, null, id, now, cancellationToken, message.Body);
            return Results.Created($"/api/messages/{message.Id}", View(message, me, now));
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
            if (message is null || await ReadableAsync(db, message.ConversationId, me, cancellationToken) is null) return NotAvailable();
            if (await db.MessageHides.FindAsync([id, me], cancellationToken) is null) db.MessageHides.Add(new MessageHide { MessageId = id, AccountId = me, HiddenAt = now });
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        api.MapPost("/conversations/{id:guid}/hide", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me, now) = (accounts.Db!, current!.Account!.Id, clock.GetUtcNow());
            if (await ReadableAsync(db, id, me, cancellationToken) is null) return NotAvailable();
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
            // D325: a restriction that names messages stops the member writing; everything else stays open.
            if (current!.Enforcement.NoMessages) return Problem(StatusCodes.Status403Forbidden, ProblemCodes.Restricted, "You can't send messages right now.");
            if (current!.Enforcement.NoPhotoUploads) return Problem(StatusCodes.Status403Forbidden, ProblemCodes.Restricted, "You can't send photos right now.");
            var (db, me, now) = (accounts.Db!, current!.Account!.Id, clock.GetUtcNow());
            // Read the bounded request body before locking the conversation; closing/blocking must not wait for an upload stream.
            if (await OpenAsync(db, id, me, cancellationToken) is null) return NotAvailable();
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
            await using var lifecycle = await AccountLifecycleLease.AcquireAsync(db, me, cancellationToken);
            if (!await db.Accounts.AsNoTracking().AnyAsync(a => a.Id == me && a.Status == AccountStatus.Active, cancellationToken)) return NotAvailable();
            await using var barrier = await Retention.PreservationLease.SharedAsync(db, cancellationToken);
            await using var transaction = await LockAsync(db, id, me, cancellationToken);
            now = clock.GetUtcNow();
            var conversation = await OpenAsync(db, id, me, cancellationToken);
            if (conversation is null) return NotAvailable();
            await db.Entry(conversation).ReloadAsync(cancellationToken);
            var other = conversation.PairLow == me ? conversation.PairHigh : conversation.PairLow;
            switch (MessagingPolicy.CanSend(await SendFactsAsync(db, current.Profile!.Bot, me, other, id, now, cancellationToken), "photo"))
            {
                case SendRefusal.NotAvailable: return NotAvailable();
                case SendRefusal.BotMayOnlyReply: return Problem(StatusCodes.Status403Forbidden, ProblemCodes.BotCannotInitiate, "Official bots reply only in conversations members start.");
                case SendRefusal.ArrivalRequired: return Problem(StatusCodes.Status403Forbidden, ProblemCodes.ArrivalRequired, "You can reply, or write first once you've confirmed you've arrived.");
                case SendRefusal.AwaitingReply: return AwaitingReply();
                case SendRefusal.RateLimited: return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.TooManyRequests, "That's a lot of messages. Slow down a little.");
            }
            if (!(await Notifications.Notifier.SettingsAsync(db, other, cancellationToken)).ChatPhotos)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.PhotosOff, "This person isn't accepting photos in chat.");
            var pipeline = accounts.Services.GetRequiredService<Media.MediaPipeline>();
            if (!pipeline.Available) return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.MediaUnavailable, "Photos aren't available right now.");
            if (await db.MediaItems.CountAsync(i => i.AccountId == me && i.Purpose == AttachmentPolicy.Purpose && i.CreatedAt > now - AttachmentPolicy.Day, cancellationToken) >= AttachmentPolicy.PerDay)
                return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.AttachmentLimit, $"You can send up to {AttachmentPolicy.PerDay} photos a day.");
            var (item, error) = await pipeline.UploadAsync(me, buffer.ToArray(), null, now, cancellationToken, conversation.Id, deferScreening: true);
            if (error != MediaUploadError.None) return error switch
            {
                MediaUploadError.TooBig => Problem(StatusCodes.Status413PayloadTooLarge, ProblemCodes.PhotoTooBig, "Photos can be up to 10 MB."),
                MediaUploadError.UnsupportedType or MediaUploadError.Animated => Problem(StatusCodes.Status415UnsupportedMediaType, ProblemCodes.PhotoUnsupported, "Use a JPEG, PNG or WebP photo."),
                MediaUploadError.TooSmall => Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhotoInvalid, $"Photos need to be at least {MediaPolicy.MinSide} pixels on each side."),
                MediaUploadError.Malware => Problem(StatusCodes.Status422UnprocessableEntity, ProblemCodes.PhotoRefused, "That file can't be used. Choose a different photo."),
                MediaUploadError.ScanUnsupported => Problem(StatusCodes.Status422UnprocessableEntity, ProblemCodes.PhotoRefused, "That photo couldn't be checked. Choose a different photo."),
                MediaUploadError.ScanUnavailable => Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.PhotoCheckUnavailable, "Photos can't be checked right now. Try again in a few minutes."),
                _ => Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhotoInvalid, "That photo couldn't be read."),
            };
            now = clock.GetUtcNow();
            // #300 (D380): an accepted photo resets the other member's run now, but they see it only once it passed. If it
            // frees them, tell their clients to read the conversation again (state only, nothing about the photo).
            var (theirLimit, theirRun) = await UnansweredAsync(db, id, other, me, cancellationToken);
            var frees = MessagingPolicy.AwaitingReply(theirLimit, theirRun);
            // The message exists from now, for the sender only; LastMessageAt moves when the recipient can see it.
            var message = new Message { Id = Guid.NewGuid(), ConversationId = id, SenderAccountId = me, Kind = MessageKind.Photo, Body = "", SentAt = now, MediaId = item!.Id };
            db.Messages.Add(message);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await barrier.DisposeAsync();
            await lifecycle.DisposeAsync();
            await MemberMutationAuthority.ReleaseForEffectsAsync(accounts.Services);
            pipeline.ScheduleScreening(item!.Id);
            var view = View(message, me, now, item);
            await BetaMessageAudience.SendAsync(accounts.Services,me,view,cancellationToken);
            if (frees) await PushReleasedAsync(hub, [(id, other)], cancellationToken);
            return Results.Accepted($"/api/messages/{message.Id}", view);
        }).DisableAntiforgery();

        // D213: the closure, mode changes, farewell, system event and audit commit together; retries are harmless.
        api.MapPost("/conversations/{id:guid}/end", async (Guid id, EndConversation request, ClaimsPrincipal user, AccountResolver accounts, IHubContext<MessagesHub> hub,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me, now) = (accounts.Db!, current!.Account!.Id, clock.GetUtcNow());
            if (!ConversationEnding.Valid(request.Closing)) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidClosing, "Choose one of the closing messages, or none.");
            await using var barrier = await Retention.PreservationLease.SharedAsync(db, cancellationToken);
            await using var transaction = await LockAsync(db, id, me, cancellationToken);
            now = clock.GetUtcNow();
            var conversation = await ReadableAsync(db, id, me, cancellationToken);
            if (conversation is null) return NotAvailable();
            if (conversation.ClosedAt is not null)
                return conversation.ClosedReason == ClosedReasons.MemberEnded ? Results.Ok(await ViewAsync(db, conversation, me, cancellationToken)) : NotAvailable();
            var other = conversation.PairLow == me ? conversation.PairHigh : conversation.PairLow;
            var events = new List<Message>();
            if (request.Closing is { } choice && MessagingPolicy.CanSend(await SendFactsAsync(db, current.Profile!.Bot, me, other, id, now, cancellationToken) with { SentLastMinute = 0, Unanswered = 0 },
                    ConversationEnding.Closings[choice]) == SendRefusal.None)
                events.Add(new Message { Id = Guid.NewGuid(), ConversationId = id, SenderAccountId = me, Kind = MessageKind.Farewell, Body = ConversationEnding.Closings[choice], SentAt = now });
            // Sender is internal provenance only: the system projection always has Mine=false and no initiating-party metadata.
            events.Add(new Message { Id = Guid.NewGuid(), ConversationId = id, SenderAccountId = me, Kind = MessageKind.System, Body = "MemberEnded", SentAt = now });
            db.Messages.AddRange(events);
            foreach (var accepted in await db.ConnectionRequests.Where(r => r.PairLow == conversation.PairLow && r.PairHigh == conversation.PairHigh
                         && r.Status == ConnectionRequestStatus.Accepted).ToListAsync(cancellationToken))
                if (ConnectionRequestPolicy.Disconnect(accepted, me, now))
                    Audit.Record(db, now, me, "account", "request.disconnected", "connection-request", accepted.Id.ToString(), accepted.ModeId);
            (conversation.ClosedAt, conversation.ClosedReason, conversation.LastMessageAt) = (now, ClosedReasons.MemberEnded, now);
            Audit.Record(db, now, me, "account", "conversation.ended", "conversation", id.ToString(), "MemberEnded");
            await CallOffMeetupsAsync(db, id, now, cancellationToken); // issue 545
            var notifier = accounts.Services.GetRequiredService<Notifications.Notifier>();
            var queuedNotification = await notifier.QueueClosureAsync(other, me, id, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await barrier.DisposeAsync();
            await MemberMutationAuthority.ReleaseForEffectsAsync(accounts.Services);
            foreach (var message in events)
            {
                await BetaMessageAudience.SendAsync(accounts.Services,me,View(message,me,now),cancellationToken);
                await BetaMessageAudience.SendAsync(accounts.Services,other,View(message,other,now),cancellationToken);
            }
            if (queuedNotification is { } notificationId) await notifier.DispatchClosureAsync(notificationId, now, cancellationToken);
            await PushClosedAsync(hub, [conversation], cancellationToken);
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

        // #185 (D341): a connection never outlives the access token it opened with, so a session ended in Keycloak (one device
        // signed out from Security & access, on any API instance) stops receiving within the token lifetime. Clients reconnect
        // with a refreshed token; a signed-out device can't refresh and stays disconnected.
        app.MapHub<MessagesHub>("/hubs/messages", options => options.CloseOnAuthenticationExpiration = true).RequireAuthorization();
    }

    private static async Task<SendFacts> SendFactsAsync(SwoovrDbContext db, bool senderIsBot, Guid me, Guid other, Guid conversation, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var (limit, unanswered) = await UnansweredAsync(db, conversation, me, other, cancellationToken);
        return new(senderIsBot, await ConnectedAsync(db, me, other, cancellationToken), await ParticipatesAsync(db, other, now, cancellationToken),
            await db.Messages.AnyAsync(m => m.ConversationId == conversation && m.SenderAccountId == other, cancellationToken),
            await TravellerBeforeArrivalAsync(db, me, other, now, cancellationToken),
            await db.Messages.CountAsync(m => m.SenderAccountId == me && m.SentAt > now.AddMinutes(-1), cancellationToken), limit, unanswered);
    }

    /// <summary>
    /// #300 (D380): the recipient's unanswered-message limit as it is now, and the sender's run of consecutive text and
    /// photo messages since the recipient last wrote one (counted only when a limit is on, and only up to it). It reads
    /// the stored messages, which delete for both, hiding, screening and moderation removal never take away, so there is
    /// no counter to drift, a reply resets it by existing, and turning the limit on again counts the run already there.
    /// The send routes call this after locking the conversation row (LockAsync), so two sends in one conversation, in
    /// either direction, are checked one after the other: concurrent sends can't pass the limit, and a refused or failed
    /// send leaves nothing behind. A preference change takes effect from the next send that reads it.
    /// </summary>
    public static async Task<(int? Limit, int Unanswered)> UnansweredAsync(SwoovrDbContext db, Guid conversation, Guid sender, Guid recipient,
        CancellationToken cancellationToken)
    {
        var limit = (await Notifications.Notifier.SettingsAsync(db, recipient, cancellationToken)).UnansweredLimit;
        return limit is { } cap ? (cap, await UnansweredCountAsync(db, conversation, sender, recipient, cap, cancellationToken)) : (null, 0);
    }

    /// <summary>#300: the sender's consecutive text, photo, meet-up and location messages after the recipient's last one, counted up to cap (index conversation_id, sent_at). PR 583 review: a meet-up invitation counts and resets the run like any message.</summary>
    public static async Task<int> UnansweredCountAsync(SwoovrDbContext db, Guid conversation, Guid sender, Guid recipient, int cap, CancellationToken cancellationToken)
    {
        var lastReply = await db.Messages.Where(m => m.ConversationId == conversation && m.SenderAccountId == recipient
                && (m.Kind == MessageKind.Text || m.Kind == MessageKind.Photo || m.Kind == MessageKind.Meetup || m.Kind == MessageKind.Location))
            .MaxAsync(m => (DateTimeOffset?)m.SentAt, cancellationToken);
        return await db.Messages.Where(m => m.ConversationId == conversation && m.SenderAccountId == sender
                && (m.Kind == MessageKind.Text || m.Kind == MessageKind.Photo || m.Kind == MessageKind.Meetup || m.Kind == MessageKind.Location) && (lastReply == null || m.SentAt > lastReply))
            .Take(cap).CountAsync(cancellationToken);
    }

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
        var links = services.GetRequiredService<Media.MediaLinks>();
        var conversation = item.Status == MediaStatus.Passed && message.DeletedAt is null ? await OpenAsync(db, message.ConversationId, message.SenderAccountId, cancellationToken) : null;
        var other = conversation is null ? Guid.Empty : conversation.PairLow == message.SenderAccountId ? conversation.PairHigh : conversation.PairLow;
        // D423 (#546): an explicit photo goes only to an eligible recipient, checked now. Otherwise it never reaches them (no
        // message, notification, count or link) and only the sender learns it wasn't delivered, in neutral words.
        if (conversation is not null && item.ContentRating == ContentRating.Explicit && message.ExplicitRefusedAt is null
            && !await ExplicitChat.AllowedAsync(db, message.SenderAccountId, other, cancellationToken))
        {
            message.ExplicitRefusedAt = now;
            Audit.Record(db, now, null, "system", "message.explicit-refused", "message", message.Id.ToString());
            await db.SaveChangesAsync(cancellationToken);
        }
        await BetaMessageAudience.SendAsync(services,message.SenderAccountId,View(message,message.SenderAccountId,now,item,links),cancellationToken);
        if (conversation is null || message.ExplicitRefusedAt is not null) return;
        conversation.LastMessageAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await BetaMessageAudience.SendAsync(services,other,View(message,other,now,item,links,explicitAllowed:true),cancellationToken);
        await services.GetRequiredService<Notifications.Notifier>().NotifyAsync(other, NotificationKind.Message, message.SenderAccountId, null, conversation.Id, now, cancellationToken);
    }

    private static async Task<IDbContextTransaction> LockAsync(SwoovrDbContext db, Guid id, Guid me, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM conversations WHERE id = {id} AND (pair_low = {me} OR pair_high = {me}) FOR UPDATE", cancellationToken);
            return transaction;
        }
        catch { await transaction.DisposeAsync(); throw; }
    }

    private static IResult NotAvailable() => Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "That conversation isn't available.");

    /// <summary>#300 (D380): neutral, never the recipient's number, never another channel. Clients say the same (Swoovr.Client.MessageText).</summary>
    public const string UnansweredLimitRefusal = "You've reached the unanswered message limit for this conversation. You can send more when they reply.";

    private static IResult AwaitingReply() => Problem(StatusCodes.Status409Conflict, ProblemCodes.UnansweredLimit, UnansweredLimitRefusal);

    /// <summary>
    /// A message for one viewer. A photo message carries a short-lived link once it passed (never after delete for both),
    /// and its state for the sender only (a blocked photo reads as not approved, procedure §4).
    /// </summary>
    /// <param name="explicitAllowed">D423: the caller checked <see cref="ExplicitChat.AllowedAsync"/> for this viewer as the
    /// recipient now; without it an explicit photo gets no link for them (the sender always sees their own).</param>
    public static MessageView View(Message message, Guid viewer, DateTimeOffset now, MediaItem? media = null, Media.MediaLinks? links = null, MeetupView? meetup = null,
        bool explicitAllowed = false)
    {
        // PR 583 review: removing a meet-up's note keeps its card and controls (the note reads "Removed by Swoovr."), so the plan
        // can still be answered and never blocks the chat's next invitation.
        if(message.ModerationRemovedAt!=null&&!(message.Kind==MessageKind.Meetup&&meetup is not null))
            return new(message.Id,message.ConversationId,message.SenderAccountId==viewer,"removed-by-moderation","Removed by Swoovr.",message.SentAt,false);
        if (message.Kind == MessageKind.System)
            return new(message.Id, message.ConversationId, false, "system", ConversationEnding.SystemText(message.SenderAccountId == viewer), message.SentAt, false);
        PhotoView? photo = null;
        string? state = null;
        var explicitPhoto = false;
        if (message.Kind == MessageKind.Photo && message.DeletedAt is null && media is not null)
        {
            var mine = message.SenderAccountId == viewer;
            explicitPhoto = media.ContentRating == ContentRating.Explicit;
            // D423: the recipient's link to an explicit photo only while they may receive it now, and never one refused at delivery.
            var shown = mine || !explicitPhoto || explicitAllowed && message.ExplicitRefusedAt is null;
            // PR 606/607 review: the recipient's link to an explicit photo is its own variant, checked again when it's served.
            if (shown && media.Status == MediaStatus.Passed && media.RemovedAt is null && links?.For(media.Id, mine || !explicitPhoto ? "member" : "explicit", now) is { } link)
                photo = new PhotoView(media.Id, link);
            if (mine) state = media.RemovedAt is not null ? "removed" : message.ExplicitRefusedAt is not null ? ExplicitChatPolicy.NotAccepted : MediaPolicy.MemberState(media.Status);
        }
        // Issue 544 (D455): a location's place, while the message still shows; the text is its label only.
        var place = message.Kind == MessageKind.Location && message.DeletedAt is null ? ChatLocationPolicy.Read(message.Body) : null;
        var text = message.Kind == MessageKind.Location ? place?.Label ?? "" : MessagingPolicy.VisibleText(message);
        return new(message.Id, message.ConversationId, message.SenderAccountId == viewer, Wire(message.Kind), text, message.SentAt,
            MessagingPolicy.MayDeleteForBoth(message, viewer, now), message.DeletedAt is not null, photo, state, meetup, explicitPhoto ? true : null,
            place is null ? null : new ChatLocationView(place.Latitude, place.Longitude, place.Label));
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
            && (c.ClosedReason == ClosedReasons.Disconnected || c.ClosedReason == ClosedReasons.Deleted || c.ClosedReason == ClosedReasons.MemberEnded)
            && !db.Blocks.Any(b => (b.BlockerAccountId == c.PairLow && b.BlockedAccountId == c.PairHigh) || (b.BlockerAccountId == c.PairHigh && b.BlockedAccountId == c.PairLow)), cancellationToken);

    /// <summary>Rule 4: an accepted connection in any mode, and no block either way.</summary>
    public static async Task<bool> ConnectedAsync(SwoovrDbContext db, Guid me, Guid other, CancellationToken cancellationToken)
    {
        var (low, high) = ConnectionRequest.Pair(me, other);
        return await db.ConnectionRequests.AnyAsync(r => r.PairLow == low && r.PairHigh == high && r.Status == ConnectionRequestStatus.Accepted, cancellationToken)
            && !await db.Blocks.AnyAsync(b => (b.BlockerAccountId == me && b.BlockedAccountId == other) || (b.BlockerAccountId == other && b.BlockedAccountId == me), cancellationToken);
    }

    /// <summary>Rule 15, D325: the other member's account is active, not suspended or terminated, and not restricted from messages.</summary>
    private static async Task<bool> ParticipatesAsync(SwoovrDbContext db, Guid other, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.Accounts.AnyAsync(a => a.Id == other && a.Status == AccountStatus.Active, cancellationToken)
        && !await db.Sanctions.AnyAsync(s => s.AccountId == other && s.LiftedAt == null && s.StartsAt <= now && (s.EndsAt == null || s.EndsAt > now)
            && (s.Kind == SanctionKind.Suspension || s.Kind == SanctionKind.Termination
                || s.Kind == SanctionKind.Restriction && (s.NoMessages || !s.HideFromDiscovery && !s.NoNewRequests && !s.NoPhotoUploads)), cancellationToken);

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

    /// <summary>The conversations the Chats page lists for this member (D143): open ones, and ones closed by a disconnect,
    /// a deletion or the member ending them; never across a block, and not hidden unless a message arrived since.</summary>
    public static IQueryable<Conversation> Listed(SwoovrDbContext db, Guid me) =>
        from c in db.Conversations
        where (c.PairLow == me || c.PairHigh == me)
              && (c.ClosedAt == null || c.ClosedReason == ClosedReasons.Disconnected || c.ClosedReason == ClosedReasons.Deleted || c.ClosedReason == ClosedReasons.MemberEnded)
              && !db.Blocks.Any(b => (b.BlockerAccountId == c.PairLow && b.BlockedAccountId == c.PairHigh) || (b.BlockerAccountId == c.PairHigh && b.BlockedAccountId == c.PairLow))
        let hidden = db.ConversationHides.Where(h => h.ConversationId == c.Id && h.AccountId == me).Select(h => (DateTimeOffset?)h.HiddenAt).FirstOrDefault()
        where hidden == null || (c.LastMessageAt != null && c.LastMessageAt > hidden)
        select c;

    /// <summary>
    /// Issue 541: the other person's messages this member hasn't read, per conversation. Counts text, photos and farewells
    /// the member can see (a photo only once it passed screening, as the thread shows it, and from the time it passed); never their own messages, system
    /// lines, deleted or moderation-removed messages, ones they hid, or anything from before they hid the chat.
    /// </summary>
    public static async Task<Dictionary<Guid, int>> UnreadAsync(SwoovrDbContext db, Guid me, IReadOnlyCollection<Guid> conversations, CancellationToken cancellationToken)
    {
        if (conversations.Count == 0) return [];
        return await (from m in UnreadMessages(db, me)
                      where conversations.Contains(m.ConversationId)
                      group m by m.ConversationId into g
                      select new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
    }

    /// <summary>The messages UnreadAsync counts, across every conversation (callers narrow it).</summary>
    public static IQueryable<Message> UnreadMessages(SwoovrDbContext db, Guid me) =>
        from m in db.Messages
        where m.SenderAccountId != me && m.Kind != MessageKind.System
              && m.DeletedAt == null && m.ModerationRemovedAt == null
              && !db.MessageHides.Any(h => h.MessageId == m.Id && h.AccountId == me)
              && (m.Kind != MessageKind.Photo || m.ExplicitRefusedAt == null && db.MediaItems.Any(i => i.Id == m.MediaId && i.Status == MediaStatus.Passed && i.RemovedAt == null))
        let read = db.ConversationReads.Where(r => r.ConversationId == m.ConversationId && r.AccountId == me).Select(r => (DateTimeOffset?)r.ReadAt).FirstOrDefault()
        let hidden = db.ConversationHides.Where(h => h.ConversationId == m.ConversationId && h.AccountId == me).Select(h => (DateTimeOffset?)h.HiddenAt).FirstOrDefault()
        // PR 578 review: a photo reaches the member when it passes (an admin's approval or the screener),
        // not when it was sent, so reading the chat while it was in review doesn't count it as read. The thread
        // shows it after a hide on the same rule.
        let passed = m.Kind == MessageKind.Photo ? db.MediaItems.Where(i => i.Id == m.MediaId).Select(i => i.ReviewedAt ?? i.ScreenedAt).FirstOrDefault() : null
        let delivered = passed != null && passed > m.SentAt ? passed.Value : m.SentAt
        where (read == null || delivered > read) && (hidden == null || delivered > hidden)
        select m;

    /// <summary>
    /// Issue 541/542: the mode the chat started in, and the thumbnail and published age for that mode, only where the
    /// profile is still open to this member in that mode (Connections' rule); otherwise the person is a name only.
    /// </summary>
    private static async Task<List<ConversationView>> SummarizeAsync(SwoovrDbContext db, IServiceProvider services, Guid me, IReadOnlyList<Conversation> rows,
        IReadOnlyList<ConversationView> views, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guid Other(Conversation c) => c.PairLow == me ? c.PairHigh : c.PairLow;
        var faces = await RequestEndpoints.FacesAsync(db, services, me,
            rows.Where(c => c.OriginModeId is not null).Select(c => (Other(c), c.OriginModeId!)).ToList(), now, cancellationToken);
        return views.Zip(rows, (view, c) =>
        {
            view = view with { ModeId = c.OriginModeId };
            return c.OriginModeId is { } mode && view.Person.Id != Guid.Empty && faces.TryGetValue((Other(c), mode), out var face)
                ? view with { Person = view.Person with { Photo = face.Photo, Age = face.Age } } : view;
        }).ToList();
    }

    /// <summary>Issue 541: the member has read the conversation up to now. Never moves back (two tabs racing keep the later time).</summary>
    private static Task MarkReadAsync(SwoovrDbContext db, Guid conversation, Guid me, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO conversation_reads (conversation_id, account_id, read_at) VALUES ({conversation}, {me}, {now}) ON CONFLICT (conversation_id, account_id) DO UPDATE SET read_at = GREATEST(conversation_reads.read_at, EXCLUDED.read_at)", cancellationToken);

    /// <summary>Issue 541: the Chats tab's badge, unread messages across every chat the Chats page lists.</summary>
    public static async Task<int> UnreadTotalAsync(SwoovrDbContext db, Guid me, CancellationToken cancellationToken) =>
        (await UnreadAsync(db, me, await Listed(db, me).Select(c => c.Id).ToListAsync(cancellationToken), cancellationToken)).Values.Sum();

    private static async Task<ConversationView> ViewAsync(SwoovrDbContext db, Conversation conversation, Guid me, CancellationToken cancellationToken, bool unanswered = false)
    {
        var otherId = conversation.PairLow == me ? conversation.PairHigh : conversation.PairLow;
        var other = await db.MemberProfiles.SingleOrDefaultAsync(p => p.AccountId == otherId, cancellationToken);
        // D170: after final deletion the other member has no profile; the conversation reads "Deleted member".
        if (other is null) return new(conversation.Id, new PersonSummary(Guid.Empty, "Deleted member", []), conversation.CreatedAt, conversation.LastMessageAt, false, conversation.ClosedAt is not null);
        var alias = conversation.OriginModeId is null ? null : await db.MemberModes.Where(m => m.AccountId == otherId && m.ModeId == conversation.OriginModeId).Select(m => m.DisplayName).SingleOrDefaultAsync(cancellationToken);
        var person = new PersonSummary(other.PublicId, ModePrivacy.DisplayName(alias, other.DisplayName), ProfileEndpoints.Badges(other, await ProfileEndpoints.BadgeOrderAsync(db, cancellationToken)));
        var canSend = conversation.ClosedAt is null && await ConnectedAsync(db, me, otherId, cancellationToken)
            && await ParticipatesAsync(db, otherId, DateTimeOffset.UtcNow, cancellationToken);
        // #300: only for the one conversation being opened, so the chat list stays a query per row as before.
        var awaiting = canSend && unanswered && await UnansweredAsync(db, conversation.Id, me, otherId, cancellationToken) is var (limit, run)
            && MessagingPolicy.AwaitingReply(limit, run);
        return new(conversation.Id, person, conversation.CreatedAt, conversation.LastMessageAt, canSend, conversation.ClosedAt is not null, awaiting);
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
            await CallOffMeetupsAsync(db, open.Id, now, cancellationToken); // issue 545
        }
        // Issue 572 (D448): a block either way removes both members' private favourites of each other.
        await db.MemberFavourites.Where(f => f.AccountId == me && f.TargetAccountId == other || f.AccountId == other && f.TargetAccountId == me).ExecuteDeleteAsync(cancellationToken);
        return open;
    }

    /// <summary>#82: tells both members' open chat windows that these conversations closed (after the change is saved).</summary>
    /// <summary>
    /// #300 (D380): the members a recipient's settings change frees. Only a looser limit (Off, or higher) frees anyone,
    /// and only those whose run had reached the old limit in an open conversation; lowering it needs no push, because
    /// the next send is refused and the client locks. Nobody else is told anything.
    /// </summary>
    public static async Task<IReadOnlyList<(Guid Conversation, Guid Member)>> ReleasedByAsync(SwoovrDbContext db, Guid recipient, int? before, int? after,
        CancellationToken cancellationToken)
    {
        if (before is not { } old || after is { } next && next <= old) return [];
        var open = await db.Conversations.Where(c => c.ClosedAt == null && (c.PairLow == recipient || c.PairHigh == recipient))
            .Select(c => new { c.Id, Other = c.PairLow == recipient ? c.PairHigh : c.PairLow }).ToListAsync(cancellationToken);
        var released = new List<(Guid, Guid)>();
        foreach (var c in open)
            if (await UnansweredCountAsync(db, c.Id, c.Other, recipient, old, cancellationToken) >= old) released.Add((c.Id, c.Other));
        return released;
    }

    /// <summary>#300: tells each freed member's clients to read that conversation again, so an open composer unlocks at once (a looser limit, or a reply they can't see yet: a photo in screening).</summary>
    public static async Task PushReleasedAsync(IHubContext<MessagesHub> hub, IEnumerable<(Guid Conversation, Guid Member)> released, CancellationToken cancellationToken)
    {
        foreach (var (conversation, member) in released)
            await hub.Clients.Group(MessagesHub.Group(member)).SendAsync("released", new ConversationReleased(conversation), cancellationToken);
    }

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
        await CallOffMeetupsAsync(db, open.Id, now, cancellationToken); // issue 545
        return open;
    }
}

/// <summary>
/// The messaging hub (D143): members only, server-to-client pushes ("message", "deleted", "closed") to each member's own
/// group. Clients send through the REST routes, so every send passes the same server-side checks.
/// </summary>
[Authorize]
public sealed class MessagesHub(AccountResolver accounts, MessagesConnections connections) : Hub
{
    public static string Group(Guid account) => "account:" + account;

    public override async Task OnConnectedAsync()
    {
        var (current, denied) = await MemberAsync(Context.User!, accounts, Context.ConnectionAborted);
        if (denied is not null || current?.Account is null) { Context.Abort(); return; }
        // #198: remember the sign-in this connection was opened with, so a later session cutoff closes it.
        var context = Context;
        connections.Add(context.ConnectionId, new(current.Account.Id, current.Facts.AuthenticatedAt, context.User!.FindFirstValue("iat"), context.Abort, context.User!.FindFirstValue("sid")));
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(current.Account.Id), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
