using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using Swoovr.Infrastructure.Ticketing;

namespace Swoovr.Api.Ticketing;

public static class TicketingWebhookEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/integrations/zammad/events", Receive).AllowAnonymous();
    }

    private static async Task<IResult> Receive(HttpContext context, IConfiguration configuration, TimeProvider clock)
    {
        var secret = configuration["Ticketing:Zammad:WebhookSecret"];
        var db = context.RequestServices.GetService<SwoovrDbContext>();
        if (string.IsNullOrEmpty(secret) || secret.Length < 32 || db is null) return Results.StatusCode(503);
        if (context.Request.ContentLength > 8192) return Results.StatusCode(413);
        var delivery = context.Request.Headers["X-Zammad-Delivery"].ToString();
        if (!Guid.TryParseExact(delivery, "D", out var deliveryGuid)) return Results.BadRequest();
        using var buffer = new MemoryStream();
        var chunk = new byte[1024];
        while (true)
        {
            var read = await context.Request.Body.ReadAsync(chunk, context.RequestAborted);
            if (read == 0) break;
            if (buffer.Length + read > 8192) return Results.StatusCode(413);
            buffer.Write(chunk, 0, read);
        }
        var body = buffer.ToArray();
        if (!ZammadSignature.Verify(body, context.Request.Headers["X-Hub-Signature"].ToString(), secret)) return Results.Unauthorized();
        var ids = Parse(body);
        if (ids is null) return Results.BadRequest();
        var canonicalDelivery = deliveryGuid.ToString("D");
        var digest = Convert.ToHexStringLower(SHA256.HashData(body));
        var previous = await db.Set<TicketingWebhookWork>().AsNoTracking().SingleOrDefaultAsync(row => row.DeliveryId == canonicalDelivery, context.RequestAborted);
        if (previous is not null) return previous.BodyDigest == digest ? Results.NoContent() : Results.Conflict();
        if (await db.Set<TicketingWebhookWork>().AnyAsync(row => row.BodyDigest == digest, context.RequestAborted)) return Results.NoContent();
        var now = clock.GetUtcNow();
        db.Add(new TicketingWebhookWork {Id = Guid.NewGuid(), DeliveryId = canonicalDelivery, BodyDigest = digest,
            ProviderTicketId = ids.Value.TicketId, ProviderArticleId = ids.Value.ArticleId, ProviderCustomerId = ids.Value.CustomerId, ReceivedAt = now, NextAttemptAt = now});
        try { await db.SaveChangesAsync(context.RequestAborted); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException {SqlState: PostgresErrorCodes.UniqueViolation})
        {
            db.ChangeTracker.Clear();
            previous = await db.Set<TicketingWebhookWork>().AsNoTracking().SingleOrDefaultAsync(row => row.DeliveryId == canonicalDelivery, context.RequestAborted);
            if (previous is not null) return previous.BodyDigest == digest ? Results.NoContent() : Results.Conflict();
            if (await db.Set<TicketingWebhookWork>().AnyAsync(row => row.BodyDigest == digest, context.RequestAborted)) return Results.NoContent();
            throw;
        }
        return Results.NoContent();
    }

    public static (long TicketId, long? ArticleId, long? CustomerId)? Parse(ReadOnlyMemory<byte> body)
    {
        try
        {
            using var document = JsonDocument.Parse(body, new JsonDocumentOptions {MaxDepth = 8});
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("ticket", out var ticket) || ticket.ValueKind != JsonValueKind.Object ||
                !ticket.TryGetProperty("id", out var id) || !Positive(id, out var ticketId) ||
                !ticket.TryGetProperty("updated_at", out var updated) || updated.ValueKind != JsonValueKind.String ||
                !ValidClock(updated.GetString())) return null;
            long? articleId = null;
            if (root.TryGetProperty("article", out var article) && article.ValueKind != JsonValueKind.Null)
            {
                if (article.ValueKind != JsonValueKind.Object || !article.TryGetProperty("id", out var articleValue)) return null;
                if (articleValue.ValueKind != JsonValueKind.Null && articleValue.GetRawText() != "\"\"")
                {
                    if (!Positive(articleValue, out var value)) return null;
                    articleId = value;
                }
            }
            long? customerId = null;
            if (ticket.TryGetProperty("customer_id", out var customer))
            {
                if (!Positive(customer, out var value)) return null;
                customerId = value;
            }
            return (ticketId, articleId, customerId);
        }
        catch (JsonException) { return null; }
    }

    private static bool ValidClock(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _) ||
        DateTimeOffset.TryParseExact(value, "yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _);

    private static bool Positive(JsonElement value, out long result)
    {
        result = 0;
        return (value.ValueKind == JsonValueKind.Number ? value.TryGetInt64(out result) : value.ValueKind == JsonValueKind.String &&
            long.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out result)) && result > 0;
    }
}
