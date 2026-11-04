using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Ticketing;

/// <summary>
/// Guest support for people without a membership (the public website is built separately). An emailed one-time code is
/// exchanged once, by POST, for a short-lived grant scoped to that guest's own requests. Ticket numbers, email addresses
/// and member IDs are never credentials. Responses never reveal whether an address is known, a member or a guest.
/// </summary>
public sealed class GuestSupport(SwoovrDbContext db, ContactHistoryKeys keys, IEmailSender email, TimeProvider clock,
    IConfiguration configuration, TicketingActorFactory actors, GuestBotCheck bot)
{
    public const string Received = "sent-if-allowed";
    private static TicketingAccessException Refuse(int status = 404, string code = "ticket-unavailable") => new(status, code);
    private byte[] Key => keys.Lookup is { Length: >= 32 } key ? key : throw Refuse(503, "guest-support-unavailable");

    public async Task<GuestCodeReceipt> RequestCodeAsync(GuestCodeRequest request, string? address, CancellationToken ct)
    {
        var normalised = EmailAddress.Normalise(request.Email ?? "") ?? throw new ArgumentException("Enter a valid email address.");
        if (!await bot.PassesAsync(request.BotToken, address, ct)) throw Refuse(400, "guest-check-failed");
        var key = Key;
        var lookup = TicketingGuestPolicy.EmailLookup(key, normalised);
        var origin = TicketingGuestPolicy.AddressLookup(key, address);
        string code;
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            // One writer per address across replicas, so the quotas below hold under concurrency.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({0x5347434B}, {BinaryPrimitives.ReadInt32BigEndian(lookup)})", ct);
            var now = clock.GetUtcNow();
            var challenges = db.Set<TicketingGuestChallenge>();
            // Over quota: the same answer as success, so limits can't be used to learn anything about an address.
            if (await challenges.CountAsync(c => c.EmailLookup == lookup && c.CreatedAt > now.AddMinutes(-1), ct) >= TicketingGuestPolicy.CodesPerMinute
                || await challenges.CountAsync(c => c.EmailLookup == lookup && c.CreatedAt > now.AddHours(-1), ct) >= TicketingGuestPolicy.CodesPerHour
                || await challenges.CountAsync(c => c.EmailLookup == lookup && c.CreatedAt > now.AddDays(-1), ct) >= TicketingGuestPolicy.CodesPerDay
                || await challenges.CountAsync(c => c.AddressLookup == origin && c.CreatedAt > now.AddHours(-1), ct) >= TicketingGuestPolicy.AddressCodesPerHour
                || await challenges.CountAsync(c => c.AddressLookup == origin && c.CreatedAt > now.AddDays(-1), ct) >= TicketingGuestPolicy.AddressCodesPerDay)
                return new(Received);
            await challenges.Where(c => c.EmailLookup == lookup && c.UsedAt == null && c.SupersededAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.SupersededAt, now), ct);
            code = TicketingGuestPolicy.Code();
            var challenge = new TicketingGuestChallenge { Id = Guid.NewGuid(), EmailLookup = lookup, AddressLookup = origin, CreatedAt = now, ExpiresAt = now + TicketingGuestPolicy.CodeLifetime };
            challenge.CodeHash = TicketingGuestPolicy.CodeHash(key, challenge.Id, code);
            challenges.Add(challenge);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        // Persisted before the external effect; no automatic resend. The code is never put in a link (no token in a URL).
        var page = configuration["Ticketing:Guest:FrontendUrl"] is { Length: > 0 } url ? $" on the Swoovr support page ({url})" : " on the Swoovr support page";
        var body = $"Your Swoovr support code is {code}. Enter it{page} within 10 minutes. Don't share it: Swoovr staff will never ask for it. If you didn't ask for this code, you can ignore this email.";
        try { await email.SendOutcomeAsync(new(normalised, "Your Swoovr support code", body), ct); }
        catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or InvalidOperationException) { }
        return new(Received);
    }

    public async Task<GuestGrant> ExchangeAsync(GuestGrantRequest request, CancellationToken ct)
    {
        var normalised = EmailAddress.Normalise(request.Email ?? "");
        if (normalised is null || !TicketingGuestPolicy.CodeShape(request.Code)) throw Refuse(400, "guest-code-invalid");
        var key = Key;
        if (keys.CurrentVersion is not { } version) throw Refuse(503, "guest-support-unavailable");
        var lookup = TicketingGuestPolicy.EmailLookup(key, normalised);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({0x5347434B}, {BinaryPrimitives.ReadInt32BigEndian(lookup)})", ct);
        var now = clock.GetUtcNow();
        var challenge = await db.Set<TicketingGuestChallenge>().Where(c => c.EmailLookup == lookup && c.SupersededAt == null)
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).FirstOrDefaultAsync(ct);
        if (challenge is null || !TicketingGuestPolicy.Check(challenge, key, request.Code, now))
        {
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            throw Refuse(400, "guest-code-invalid");
        }
        var guest = await db.Set<TicketingGuest>().SingleOrDefaultAsync(g => g.EmailLookup == lookup, ct);
        if (guest is null)
        {
            guest = new TicketingGuest { Id = Guid.NewGuid(), EmailLookup = lookup, State = TicketingOperationState.Pending, CreatedAt = now };
            db.Add(guest);
        }
        if (guest.DeactivatedAt is not null) { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); throw Refuse(400, "guest-code-invalid"); }
        guest.EmailProtected = ContactHistoryPolicy.Encrypt(version, keys.Encryption[version], normalised);
        guest.UpdatedAt = now;
        // Keep at most a few live grants per guest; the oldest are revoked first.
        var live = await db.Set<TicketingGuestGrant>().Where(g => g.GuestId == guest.Id && g.RevokedAt == null && g.ExpiresAt > now).OrderByDescending(g => g.CreatedAt).ToListAsync(ct);
        foreach (var old in live.Skip(TicketingGuestPolicy.ActiveGrants - 1)) old.RevokedAt = now;
        var raw = TicketingGuestPolicy.Token();
        var expiry = now + TicketingGuestPolicy.GrantLifetime;
        db.Add(new TicketingGuestGrant { Id = Guid.NewGuid(), GuestId = guest.Id, TokenHash = TicketingGuestPolicy.TokenHash(raw), CreatedAt = now, ExpiresAt = expiry });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(raw, expiry);
    }

    /// <summary>The grant from `Authorization: SwoovrGuest {grant}`. Never a cookie, query string or member session.</summary>
    public static string? Presented(HttpContext context) =>
        context.Request.Headers.Authorization.Count == 1 && context.Request.Headers.Authorization[0] is { } value && value.StartsWith(TicketingGuestPolicy.Scheme + " ", StringComparison.Ordinal)
            && TicketingGuestPolicy.TokenShape(value[(TicketingGuestPolicy.Scheme.Length + 1)..]) ? value[(TicketingGuestPolicy.Scheme.Length + 1)..] : null;

    public async Task<TicketingGuest> GuestAsync(string? grant, CancellationToken ct)
    {
        if (grant is null) throw Refuse(401, "guest-grant-required");
        var hash = TicketingGuestPolicy.TokenHash(grant);
        var now = clock.GetUtcNow();
        var guest = await (from g in db.Set<TicketingGuestGrant>() join guestRow in db.Set<TicketingGuest>() on g.GuestId equals guestRow.Id
                           where g.TokenHash == hash && g.RevokedAt == null && g.ExpiresAt > now && guestRow.DeactivatedAt == null select guestRow).SingleOrDefaultAsync(ct);
        return guest ?? throw Refuse(401, "guest-grant-required");
    }

    public async Task RevokeAsync(string? grant, CancellationToken ct)
    {
        if (grant is null) throw Refuse(401, "guest-grant-required");
        var hash = TicketingGuestPolicy.TokenHash(grant);
        await db.Set<TicketingGuestGrant>().Where(g => g.TokenHash == hash && g.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(g => g.RevokedAt, clock.GetUtcNow()), ct);
    }

    private async Task<(ITicketingWorkspaceProvider Actor, long Customer)> ActorAsync(TicketingGuest guest, CancellationToken ct)
    {
        if (guest is not { State: TicketingOperationState.Applied, ProviderUserId: long customer }) throw Refuse();
        return (await actors.GuestAsync(guest.Id, customer, ct), customer);
    }

    private static async Task<SupportTicket> OwnAsync(ITicketingWorkspaceProvider actor, long customer, long id, CancellationToken ct)
    {
        var ticket = await actor.GetTicketAsync(id, ct);
        // Another guest's or member's request answers exactly like a request that doesn't exist.
        return ticket is not null && ticket.CustomerId == customer && ticket.OperationReference is not null ? ticket : throw Refuse();
    }

    public async Task<TicketPage> ListAsync(TicketingGuest guest, int page, CancellationToken ct)
    {
        if (page is < 1 or > 100) throw new ArgumentException("Invalid page.");
        if (guest.ProviderUserId is null) return new([], page, 25, false);
        var (actor, customer) = await ActorAsync(guest, ct);
        var rows = await actor.SearchAsync("customer_id:" + customer, page, 26, "updated_at", true, ct);
        var own = rows.Where(r => r.CustomerId == customer && r.OperationReference is not null).ToArray();
        return new(own.Take(25).Select(TicketingRuntime.Summary).ToArray(), page, 25, own.Length > 25);
    }

    public async Task<TicketSummary> CreateAsync(TicketingGuest guest, GuestTicketCreate request, CancellationToken ct)
    {
        if (request.OperationId == Guid.Empty || !TicketingGuestPolicy.Categories.TryGetValue(request.Category ?? "", out var groupName)) throw new ArgumentException("Choose a category.");
        Text(request.Title, 200); Text(request.Body, 16000);
        var now = clock.GetUtcNow();
        var replay = await db.Set<TicketingOperation>().AnyAsync(o => o.Id == request.OperationId, ct);
        if (!replay && await db.Set<TicketingOperation>().CountAsync(o => o.AccountId == guest.Id && o.Kind == "create-ticket" && o.CreatedAt > now.AddDays(-1), ct) >= TicketingGuestPolicy.CreatesPerDay)
            throw Refuse(429, "guest-limit-reached");
        var identity = actors.Identity();
        var operations = new TicketingOperations(db, identity, identity, clock);
        var customer = await operations.EnsureGuestCustomerAsync(guest.Id, ct);
        await db.Entry(guest).ReloadAsync(ct);
        var (actor, _) = await ActorAsync(guest, ct);
        var group = (await actor.DirectoryAsync(ct)).Groups.SingleOrDefault(g => g.Name == groupName) ?? throw Refuse(503, "ticket-provider-unavailable");
        var ticket = await new TicketingOperations(db, identity, actor, clock).CreateTicketAsync(guest.Id, request.OperationId,
            new(request.Title.Trim(), request.Body.Trim(), customer, group.Id, request.OperationId.ToString("D"), "guest"), ct);
        return TicketingRuntime.Summary(ticket);
    }

    public async Task<TicketDetail> DetailAsync(TicketingGuest guest, long id, CancellationToken ct)
    {
        var (actor, customer) = await ActorAsync(guest, ct);
        var ticket = await OwnAsync(actor, customer, id, ct);
        var articles = await actor.GetArticlesAsync(id, ct);
        ticket = await OwnAsync(actor, customer, id, ct);
        var views = articles.Where(a => a.TicketId == id && a.CustomerVisible).Select(a => new TicketArticle(a.Id, TicketingProjection.Plain(a.Body, a.ContentType), false, a.Sender, a.CreatedAt,
            a.Attachments.Where(x => x.ContentType == "text/plain" && x.Size <= 65536).Select(x => new TicketAttachment(x.Id, TicketingProjection.Filename(x.Filename), x.ContentType, x.Size)).ToArray())).ToArray();
        var directory = await actor.DirectoryAsync(ct);
        var kind = directory.States.SingleOrDefault(s => s.Id == ticket.StateId)?.Kind;
        var canReply = kind is not null and not ("merged" or "removed") && (kind != "closed" || directory.Groups.SingleOrDefault(g => g.Id == ticket.GroupId)?.FollowUpPolicy == "yes");
        return new(TicketingRuntime.Summary(ticket), views, null, canReply);
    }

    public async Task<TicketDetail> ReplyAsync(TicketingGuest guest, long id, TicketReply request, CancellationToken ct)
    {
        if (request.Private) throw Refuse(403);
        Text(request.Body, 16000);
        var uploads = (request.Attachments ?? []).Select(a => { TicketingProjection.ValidateAttachment(a.Filename, a.ContentType, a.Data); return new SupportUpload(TicketingProjection.Filename(a.Filename), a.ContentType, a.Data); }).ToArray();
        if (uploads.Length > 3 || request.OperationId == Guid.Empty) throw new ArgumentException("Invalid reply.");
        var (actor, customer) = await ActorAsync(guest, ct);
        var detail = await DetailAsync(guest, id, ct);
        if (!detail.CanReply) throw Refuse(409, "ticket-follow-up-unavailable");
        var digest = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
        var now = clock.GetUtcNow();
        var operation = await db.Set<TicketingOperation>().SingleOrDefaultAsync(o => o.Id == request.OperationId, ct);
        if (operation is not null)
        {
            if (operation.AccountId != guest.Id || operation.Kind != "reply" || operation.RequestDigest != digest || operation.ProviderTicketId != id) throw Refuse(409, "ticket-operation-conflict");
            if (operation.State == TicketingOperationState.Applied) return detail;
            if (operation.State == TicketingOperationState.Rejected) throw Refuse(409, "ticket-reconciliation-required");
            // Uncertain or interrupted: find the exact marker written by this guest's customer; never resend.
            var matches = (await actor.GetArticlesAsync(id, ct)).Where(a => a.TicketId == id && a.OperationReference == operation.Id.ToString("D") && a.AuthorId == customer && !a.Internal).ToArray();
            if (matches.Length != 1) throw Refuse(409, "ticket-reconciliation-required");
            (operation.State, operation.ProviderArticleId, operation.UpdatedAt) = (TicketingOperationState.Applied, matches[0].Id, now);
            await db.SaveChangesAsync(ct);
            return await DetailAsync(guest, id, ct);
        }
        if (await db.Set<TicketingOperation>().CountAsync(o => o.AccountId == guest.Id && o.Kind == "reply" && o.CreatedAt > now.AddHours(-1), ct) >= TicketingGuestPolicy.RepliesPerHour)
            throw Refuse(429, "guest-limit-reached");
        operation = new TicketingOperation { Id = request.OperationId, AccountId = guest.Id, Kind = "reply", RequestDigest = digest, ProviderTicketId = id, State = TicketingOperationState.Pending, CreatedAt = now, UpdatedAt = now };
        db.Add(operation);
        await db.SaveChangesAsync(ct);
        try
        {
            var article = await actor.ReplyWithOperationAsync(id, request.Body.Trim(), false, true, uploads, operation.Id.ToString("D"), ct);
            (operation.State, operation.ProviderArticleId) = (TicketingOperationState.Applied, article.Id);
        }
        catch (TicketingProviderException error) { operation.State = error.Uncertain ? TicketingOperationState.Uncertain : TicketingOperationState.Rejected; await FinishAsync(operation); throw; }
        await FinishAsync(operation);
        return await DetailAsync(guest, id, ct);
    }

    // The outcome is saved even if the caller has gone, so a later resend is reconciled rather than repeated.
    private async Task FinishAsync(TicketingOperation operation)
    {
        operation.UpdatedAt = clock.GetUtcNow();
        using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await db.SaveChangesAsync(recovery.Token);
    }

    public async Task<TicketDownload> DownloadAsync(TicketingGuest guest, long id, long article, long attachment, CancellationToken ct)
    {
        var detail = await DetailAsync(guest, id, ct);
        var item = detail.Articles.SingleOrDefault(a => a.Id == article)?.Attachments.SingleOrDefault(a => a.Id == attachment) ?? throw Refuse();
        var (actor, customer) = await ActorAsync(guest, ct);
        var data = await actor.DownloadAsync(id, article, attachment, ct);
        await OwnAsync(actor, customer, id, ct);
        TicketingProjection.ValidateAttachment(item.Filename, data.ContentType, data.Data);
        return new(item.Filename, "text/plain", data.Data);
    }

    private static void Text(string? value, int maximum) { if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Contains('\0')) throw new ArgumentException("Invalid support text."); }
}

/// <summary>
/// Optional Cloudflare Turnstile check for code requests (`Ticketing:Guest:Turnstile:Secret`). Not configured: no check is
/// made and none is claimed. Configured: a missing or rejected token fails, and so does an unreachable verifier.
/// </summary>
public sealed class GuestBotCheck(IConfiguration configuration, IHttpClientFactory clients)
{
    public const string Client = "guest-bot-check";
    public bool Required => configuration["Ticketing:Guest:Turnstile:Secret"] is { Length: > 0 };
    public async Task<bool> PassesAsync(string? token, string? address, CancellationToken ct)
    {
        if (configuration["Ticketing:Guest:Turnstile:Secret"] is not { Length: > 0 } secret) return true;
        if (token is not { Length: > 0 and <= 2048 }) return false;
        try
        {
            var form = new Dictionary<string, string> { ["secret"] = secret, ["response"] = token };
            if (address is not null) form["remoteip"] = address;
            using var response = await clients.CreateClient(Client).PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", new FormUrlEncodedContent(form), ct);
            if (!response.IsSuccessStatusCode) return false;
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            return body.RootElement.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException) { return false; }
    }
}
