using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Connections;

/// <summary>
/// Issue 544 (D431, D455): share one chosen location in a chat. It's a "location" message, so it has every send rule of the
/// chat (connected, open, restrictions, rate and unanswered limits), live delivery, the notification ("Location", never the
/// place), unread counts, delete for both, hide, report and retention, exactly as a text message. The place is the exact
/// point the member chose; Swoovr never reads, stores or shares the device's location by itself, and no map provider is
/// contacted (the card opens the member's own maps app when they tap it). The audit names the action only.
/// </summary>
public static partial class MessagingEndpoints
{
    /// <summary>Notification and list wording for a shared location: never the place itself.</summary>
    public const string LocationPreview = "Location";

    public static void MapLocations(WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapPost("/conversations/{id:guid}/locations", ShareLocationAsync);
    }

    private static async Task<IResult> ShareLocationAsync(Guid id, ShareLocation request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
        if (denied is not null) return denied;
        if (current!.Enforcement.NoMessages) return Problem(StatusCodes.Status403Forbidden, ProblemCodes.Restricted, "You can't send messages right now.");
        switch (ChatLocationPolicy.Check(request.Latitude, request.Longitude, request.Label))
        {
            case ChatLocationRefusal.None: break;
            case ChatLocationRefusal.InvalidLabel:
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidLocation, $"Keep the label to one line of up to {ChatLocationPolicy.LabelMax} characters.");
            default: return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidLocation, "Choose a place with a valid latitude and longitude.");
        }
        var body = ChatLocationPolicy.Format(request.Latitude!.Value, request.Longitude!.Value, request.Label);
        var (db, me, now) = (accounts.Db!, current.Account!.Id, clock.GetUtcNow());
        await using var lifecycle = await AccountLifecycleLease.AcquireAsync(db, me, cancellationToken);
        if (!await db.Accounts.AsNoTracking().AnyAsync(a => a.Id == me && a.Status == AccountStatus.Active, cancellationToken)) return NotAvailable();
        await using var barrier = await Retention.PreservationLease.SharedAsync(db, cancellationToken);
        await using var transaction = await LockAsync(db, id, me, cancellationToken);
        now = clock.GetUtcNow();
        var conversation = await OpenAsync(db, id, me, cancellationToken);
        if (conversation is null) return NotAvailable();
        var other = conversation.PairLow == me ? conversation.PairHigh : conversation.PairLow;
        switch (MessagingPolicy.CanSend(await SendFactsAsync(db, current.Profile!.Bot, me, other, id, now, cancellationToken), body))
        {
            case SendRefusal.None: break;
            case SendRefusal.BotMayOnlyReply: return Problem(StatusCodes.Status403Forbidden, ProblemCodes.BotCannotInitiate, "Official bots reply only in conversations members start.");
            case SendRefusal.ArrivalRequired: return Problem(StatusCodes.Status403Forbidden, ProblemCodes.ArrivalRequired, "You can reply, or write first once you've confirmed you've arrived.");
            case SendRefusal.AwaitingReply: return AwaitingReply();
            case SendRefusal.RateLimited: return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.TooManyRequests, "That's a lot of messages. Slow down a little.");
            default: return NotAvailable();
        }
        var message = new Message { Id = Guid.NewGuid(), ConversationId = id, SenderAccountId = me, Kind = MessageKind.Location, Body = body, SentAt = now };
        // The label is member text: screened like any message (the whole text, so the screener can match it to the message).
        if (ChatLocationPolicy.CleanLabel(request.Label) is not null)
            accounts.Services.GetRequiredService<Moderation.TextHarmQueue>().Enqueue(me, TextHarmSource.Message, message.Id, null, message.Body, now);
        db.Messages.Add(message);
        conversation.LastMessageAt = now;
        Audit.Record(db, now, me, "account", "message.location", "message", message.Id.ToString());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await barrier.DisposeAsync();
        await lifecycle.DisposeAsync();
        await MemberMutationAuthority.ReleaseForEffectsAsync(accounts.Services);
        await BetaMessageAudience.SendAsync(accounts.Services, me, View(message, me, now), cancellationToken);
        await BetaMessageAudience.SendAsync(accounts.Services, other, View(message, other, now), cancellationToken);
        await accounts.Services.GetRequiredService<Notifications.Notifier>().NotifyAsync(other, NotificationKind.Message, me, null, id, now, cancellationToken, LocationPreview);
        return Results.Created($"/api/messages/{message.Id}", View(message, me, now));
    }
}
