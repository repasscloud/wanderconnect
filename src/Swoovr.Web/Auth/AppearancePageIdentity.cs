using System.Security.Claims;
using Swoovr.Contracts;
namespace Swoovr.Web.Auth;

/// <summary>Private cache namespace from the already authenticated member cookie; no tokens/preferences enter SSR.</summary>
public static class AppearancePageIdentity
{
    public static string? Scope(ClaimsPrincipal? user,IConfiguration configuration)
    {
        if(user?.Identity?.IsAuthenticated!=true)return null;
        var subject=user.FindFirstValue("sub")??user.FindFirstValue(ClaimTypes.NameIdentifier);
        var issuer=user.FindFirstValue("iss")??configuration["Keycloak:Authority"]?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(subject)||string.IsNullOrWhiteSpace(issuer)?null:AppearanceOwner.ForIdentity(issuer,subject);
    }
}
