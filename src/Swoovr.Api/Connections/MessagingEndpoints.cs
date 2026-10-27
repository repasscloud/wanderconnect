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
            var rows = await db.Messages.Where(m => m.ConversationId == id && (before == null || m.SentAt < before)
                    && (hiddenAt == null || m.SentAt > hiddenAt) && !db.MessageHides.Any(h => h.MessageId == m.Id && h.AccountId == me))
                .OrderByDescending(m => m.SentAt).ThenByDescending(m => m.Id).Take(MessagingPolicy.PageSize + 1).ToListAsync(cancellationToken);
            var page = rows.Take(MessagingPolicy.PageSize).ToList();
            return Results.Ok(new MessagePage(page.Select(m => View(m, me, now)).ToList(), rows.Count > MessagingPolicy.PageSize, page.LastOrDefault()?.SentAt));
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
            var facts = new SendFacts(current.Profile!.Bot, await ConnectedAsync(db, me, other, cancellationToken), await ParticipatesAsync(db, other, now, cancellationToken),
                await db.Messages.AnyAsync(m => m.ConversationId == id && m.SenderAccountId == other, cancellationToken),
                await TravellerBeforeArrivalAsync(db, me, other, now, cancellationToken),
                await db.Messages.CountAsync(m => m.SenderAccountId == me && m.SentAt > now.AddMinutes(-1), cancellationToken));
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

        app.MapHub<MessagesHub>("/hubs/messages").RequireAuthorization();
    }

    private static IResult NotAvailable() => Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "That conversation isn't available.");

    public static MessageView View(Message message, Guid viewer, DateTimeOffset now) =>
        new(message.Id, message.ConversationId, message.SenderAccountId == viewer, Wire(message.Kind), MessagingPolicy.VisibleText(message), message.SentAt,
            MessagingPolicy.MayDeleteForBoth(message, viewer, now), message.DeletedAt is not null);

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
