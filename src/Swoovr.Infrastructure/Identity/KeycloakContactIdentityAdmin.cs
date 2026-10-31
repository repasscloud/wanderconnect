using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Swoovr.Domain;

namespace Swoovr.Infrastructure.Identity;

/// <summary>Composition supplies the canonical contract and trusted public member issuer; transport may use private DNS.</summary>
public sealed record ContactPolicyContract(string Header, string Version, string Issuer, bool Enabled);

public sealed partial class KeycloakIdentityAdmin
{
    private const int ContactResponseMax = 32 * 1024;
    public bool ContactRetirementConfigured => Configured && contactPolicy is {Enabled:true}
        && !string.IsNullOrWhiteSpace(contactPolicy.Header) && !string.IsNullOrWhiteSpace(contactPolicy.Version)
        && !string.IsNullOrWhiteSpace(contactPolicy.Issuer);
    public string ContactIssuer => contactPolicy?.Issuer ?? "";

    public async Task<IdentityEmailBindingResult> ReadEmailBindingAsync(Guid subject,CancellationToken ct)
    {
        if(!ContactRetirementConfigured)return new(IdentityContactState.NotSupported);
        if(subject==Guid.Empty)return new(IdentityContactState.Unavailable);
        if(!await ContactReadyAsync(ct))return new(IdentityContactState.Unavailable);
        var reply=await ContactCallAsync(HttpMethod.Get,$"users/{subject}/email-binding",null,ct);
        if(reply.Status!=HttpStatusCode.OK||reply.Data is not {} body)return new(ContactFailure(reply));
        if(!GuidValue(body,"userId",out var returned)||returned!=subject||!StringValue(body,"email",out var email,true)
            ||!StringValue(body,"username",out var username,true)||!BoolValue(body,"emailVerified",out var verified)||!BoolValue(body,"enabled",out var enabled)
            ||email?.Length>254||username?.Length>255)return new(IdentityContactState.Unavailable);
        return new(IdentityContactState.Available,new(subject,email,username,verified,enabled));
    }

    public async Task<IdentityContactState> EmailRetirementStatusAsync(Guid subject,Guid operation,CancellationToken ct)
    {
        if(!ContactRetirementConfigured)return IdentityContactState.NotSupported;
        if(subject==Guid.Empty||operation==Guid.Empty||!await ContactReadyAsync(ct))return IdentityContactState.Unavailable;
        return await ContactStatusAsync(subject,operation,ct);
    }

    public async Task<IdentityContactState> RetireEmailBindingAsync(IdentityEmailBinding expected,Guid operation,CancellationToken ct)
    {
        if(!ContactRetirementConfigured)return IdentityContactState.NotSupported;
        if(expected.Subject==Guid.Empty||operation==Guid.Empty||string.IsNullOrWhiteSpace(expected.Email)||expected.Email.Length>254
            ||string.IsNullOrWhiteSpace(expected.Username)||expected.Username.Length>255||!await ContactReadyAsync(ct))return IdentityContactState.Unavailable;
        var attempt=await ContactCallAsync(HttpMethod.Post,$"users/{expected.Subject}/retire-email",
            new {operationId=operation,expectedEmail=expected.Email,expectedUsername=expected.Username},ct);
        // An attempt response, including HTTP200, is not proof of committed current state. Network failure is ambiguous.
        var confirmed=await ContactStatusAsync(expected.Subject,operation,ct);
        return confirmed==IdentityContactState.Retired?confirmed
            :confirmed==IdentityContactState.NotRecorded?ContactFailure(attempt):confirmed;
    }

    private async Task<bool> ContactReadyAsync(CancellationToken ct)
    {
        var reply=await ContactCallAsync(HttpMethod.Get,"capabilities",null,ct);
        return reply.Status==HttpStatusCode.OK&&reply.Data is {} body
            &&StringValue(body,"issuer",out var issuer)&&issuer==ContactIssuer
            &&StringValue(body,"realm",out var realm)&&realm==options.Realm
            &&body.TryGetProperty("schema",out var schema)&&schema.ValueKind==JsonValueKind.Number&&schema.TryGetInt32(out var version)&&version==1
            &&BoolValue(body,"retireEmail",out var enabled)&&enabled;
    }

    private async Task<IdentityContactState> ContactStatusAsync(Guid subject,Guid operation,CancellationToken ct)
    {
        var reply=await ContactCallAsync(HttpMethod.Get,$"users/{subject}/retire-email/{operation}",null,ct);
        if(reply.Status!=HttpStatusCode.OK||reply.Data is not {} body)return ContactFailure(reply);
        return StringValue(body,"status",out var status)&&status=="retired"&&GuidValue(body,"userId",out var returned)&&returned==subject
            &&GuidValue(body,"operationId",out var recorded)&&recorded==operation?IdentityContactState.Retired:IdentityContactState.Unavailable;
    }

    private sealed record ContactReply(HttpStatusCode? Status,JsonElement? Data);
    private async Task<ContactReply> ContactCallAsync(HttpMethod method,string path,object? body,CancellationToken ct)
    {
        try
        {
            var bearer=await TokenAsync(ct);if(bearer is null)return new(null,null);
            using var request=new HttpRequestMessage(method,$"{options.BaseUrl.TrimEnd('/')}/realms/{Uri.EscapeDataString(options.Realm)}/swoovr-contact-policy/{path}");
            request.Headers.Authorization=new("Bearer",bearer);
            request.Headers.Add(contactPolicy!.Header,contactPolicy.Version);
            if(body is not null)request.Content=JsonContent.Create(body);
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
            if(response.StatusCode==HttpStatusCode.Unauthorized)token=null;
            if(!response.Headers.TryGetValues(contactPolicy.Header,out var versionValues))return new(null,null);
            var versions=versionValues.ToArray();if(versions.Length!=1||versions[0]!=contactPolicy.Version)return new(null,null);
            if(!string.Equals(response.Content.Headers.ContentType?.MediaType,"application/json",StringComparison.OrdinalIgnoreCase))return new(null,null);
            await response.Content.LoadIntoBufferAsync(ContactResponseMax,ct);
            await using var stream=await response.Content.ReadAsStreamAsync(ct);
            using var document=await JsonDocument.ParseAsync(stream,new JsonDocumentOptions{MaxDepth=8},ct);
            if(document.RootElement.ValueKind!=JsonValueKind.Object)return new(null,null);
            var names=document.RootElement.EnumerateObject().Select(p=>p.Name).ToArray();
            if(names.Distinct(StringComparer.Ordinal).Count()!=names.Length)return new(null,null);
            return new(response.StatusCode,document.RootElement.Clone());
        }
        catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
        catch(Exception error)when(error is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            // Provider exception messages/error bodies may echo contacts, credentials or tokens. Never log them.
            log.LogWarning("Keycloak contact-policy request unavailable ({Kind}).",error.GetType().Name);
            return new(null,null);
        }
    }
    private static IdentityContactState ContactFailure(ContactReply reply)
    {
        if(reply.Data is {} body&&StringValue(body,"code",out var code))return (reply.Status,code) switch
        {
            (HttpStatusCode.NotFound,"retirement-not-found")=>IdentityContactState.NotRecorded,
            (HttpStatusCode.NotFound,"contact-policy-not-enabled")=>IdentityContactState.NotSupported,
            (HttpStatusCode.Conflict,"binding-changed")=>IdentityContactState.BindingChanged,
            (HttpStatusCode.Conflict,"another-retirement-recorded")=>IdentityContactState.Conflict,
            (HttpStatusCode.Conflict,"unsupported-source")=>IdentityContactState.NotSupported,
            _=>IdentityContactState.Unavailable,
        };
        return IdentityContactState.Unavailable;
    }
    private static bool StringValue(JsonElement body,string name,out string? value,bool nullable=false)
    {
        value=null;if(!body.TryGetProperty(name,out var property))return false;
        if(nullable&&property.ValueKind==JsonValueKind.Null)return true;
        if(property.ValueKind!=JsonValueKind.String)return false;
        value=property.GetString();return value is not null;
    }
    private static bool BoolValue(JsonElement body,string name,out bool value)
    {
        value=false;if(!body.TryGetProperty(name,out var property)||property.ValueKind is not(JsonValueKind.True or JsonValueKind.False))return false;
        value=property.GetBoolean();return true;
    }
    private static bool GuidValue(JsonElement body,string name,out Guid value)
    {
        value=default;return StringValue(body,name,out var text)&&Guid.TryParse(text,out value)&&value!=Guid.Empty;
    }
}
