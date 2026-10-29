using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Retention;

/// <summary>Authorised moderation writes commit before disposal; external notification effects release the request barrier.</summary>
public sealed class ModerationLifecycleFilter : IEndpointFilter
{
    public static PreservationLease? Current(HttpContext context) => context.Items[typeof(ModerationLifecycleFilter)] as PreservationLease;
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var resolver = http.RequestServices.GetRequiredService<AccountResolver>();
        var (current, denied) = await AdminEndpoints.StaffAsync(http.User, resolver, AccountRole.Support, http.RequestAborted);
        if (denied is not null) return denied;
        if (http.RequestServices.GetService<SwoovrDbContext>() is not { } db) return await next(context);
        await using var barrier = HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)
            ? await PreservationLease.SharedAsync(db, http.RequestAborted) : await PreservationLease.ExclusiveAsync(db, http.RequestAborted);
        http.Items[typeof(ModerationLifecycleFilter)] = barrier;
        try
        {
            if (!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method)
                && Guid.TryParse(http.Request.RouteValues["id"]?.ToString(), out var id))
            {
                var (primary, subjects) = await SubjectsAsync(db, (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "", id, current!.StaffRole, http.RequestAborted);
                // Existing current-own-account refusals keep their established response; the handlers already deny them.
                var currentOwn = primary is { } owner && (owner == current.Account!.Id || await db.StaffMemberLinks.AnyAsync(l => l.StaffAccountId == current.Account!.Id && l.MemberAccountId == owner, http.RequestAborted));
                if (!currentOwn && await StaffAccessEndpoints.ExcludedAsync(db, current.Account!.Id, subjects, http.RequestAborted))
                {
                    Audit.Record(db, http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(), current.Account.Id, "account", "moderation.conflict-refused", "moderation-resource", id.ToString());
                    await db.SaveChangesAsync(http.RequestAborted);
                    return StaffAccessEndpoints.Conflict();
                }
            }
            return await next(context);
        }
        finally { http.Items.Remove(typeof(ModerationLifecycleFilter)); }
    }

    private static async Task<(Guid? Primary, List<Guid> Subjects)> SubjectsAsync(SwoovrDbContext db, string path, Guid id, AccountRole role, CancellationToken token)
    {
        Guid? primary = null, review = null;
        if (path.StartsWith("/api/admin/cases/", StringComparison.OrdinalIgnoreCase))
        {
            var item = await db.ModerationCases.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && (role == AccountRole.Admin || c.Queue == CaseQueue.Support), token);
            if (item is not null) (primary, review) = (item.SubjectAccountId, item.Id);
        }
        else if (path.StartsWith("/api/admin/sanctions/", StringComparison.OrdinalIgnoreCase))
        {
            var item = await db.Sanctions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, token);
            if (item is not null) (primary, review) = (item.AccountId, item.CaseId);
        }
        else if (path.StartsWith("/api/admin/appeals/", StringComparison.OrdinalIgnoreCase))
        {
            var item = await (from a in db.Appeals join s in db.Sanctions on a.SanctionId equals s.Id where a.Id == id select new { s.AccountId, s.CaseId }).SingleOrDefaultAsync(token);
            if (item is not null) (primary, review) = (item.AccountId, item.CaseId);
        }
        else if (path.StartsWith("/api/admin/accounts/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/sanctions", StringComparison.OrdinalIgnoreCase)) primary = id;
        var subjects = review is { } caseId ? await SafetyRetention.CaseAccountsAsync(db, caseId, token) : [];
        if (primary is { } subject && !subjects.Contains(subject)) subjects.Add(subject);
        return (primary, subjects);
    }
}
