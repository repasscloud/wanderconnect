using Microsoft.Extensions.Logging;
using Swoovr.Domain;

namespace Swoovr.Infrastructure.Identity;

public sealed record StaffIdentityAdminOptions(string BaseUrl, string Realm, string ClientId, string? ClientSecret);
public sealed class KeycloakStaffIdentityAdmin(HttpClient http, StaffIdentityAdminOptions options, TimeProvider clock, ILogger<KeycloakIdentityAdmin> log)
    : KeycloakIdentityAdmin(http, new IdentityAdminOptions(options.BaseUrl, options.Realm, options.ClientId, options.ClientSecret), clock, log), IStaffIdentityAdmin;
