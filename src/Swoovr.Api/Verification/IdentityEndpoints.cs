using System.Security.Claims;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Verification;

/// <summary>
/// The controlled decryption pathway for verified identity records (policy §2.3, §11, §16; D191): one record at a time,
/// `legal` role only, with a recorded purpose in `restricted.identity_accesses` and an audit entry holding ids only.
/// There is no route that lists or searches records, and members never reach another member's legal identity.
/// </summary>
public static class IdentityEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/admin/accounts/{id:guid}/identity", async (Guid id, IdentityAccessRequest request, ClaimsPrincipal user, AccountResolver accounts,
            VerifiedIdentityStore identities, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var current = await accounts.ResolveAsync(user, cancellationToken);
            if (current is null) return Results.Unauthorized();
            if (current.Account is null) return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.PersistenceUnavailable, "Accounts are not available.");
            if (!current.Allows(AccountRole.Legal)) return Results.Forbid();
            if (!VerifiedIdentityPolicy.MayDecrypt(AccountRole.Legal, request.Purpose))
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.PurposeRequired, "Record why you need this record (up to 500 characters).");
            if (!identities.Configured) return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.NotConfigured, "The identity store isn't configured.");
            var record = await accounts.Db!.VerifiedIdentities.FindAsync([id], cancellationToken);
            var fields = record is null ? null : await identities.OpenForAsync(id, current.Account.Id, request.Purpose!, clock.GetUtcNow(), cancellationToken);
            if (record is null || fields is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "No verified identity is on file for that account.");
            return Results.Ok(new VerifiedIdentityView(id, fields.FirstName, fields.LastName, fields.DateOfBirth, fields.DocumentType, fields.IssuingCountry,
                fields.ExpiryDate, fields.Method, record.VerifiedAt, record.SessionId, record.RetainUntil));
        }).RequireAuthorization();
    }
}
