using System.Net;
using System.Text;
using Swoovr.Client;
using Swoovr.Contracts;
using System.Text.Json;

if(args is ["--contact-provider-probe"])
{
    await ContactIdentityProviderProbe.Run();
    return;
}

MeetingSafetyChecks.Run();
MeetingSafetyContactAccessChecks.Run();
BannedIdentifierChecks.Run();
FallbackKeyChecks.Run();
ApiContractChecks.Run();
AccessPolicyChecks.Run();
AccountPolicyChecks.Run();
DiscoveryPolicyChecks.Run();
ConnectionModeChecks.Run();
ConnectionRequestChecks.Run();
TravelChecks.Run();
MarketChecks.Run();
VerificationChecks.Run();
ModerationChecks.Run();
MatchingChecks.Run();
AppParityChecks.Run();
ArrivalChecks.Run();
MessagingChecks.Run();
DobCorrectionChecks.Run();
DeletionChecks.Run();
LegalHoldChecks.Run();
SafetyRetentionChecks.Run();
LawEnforcementChecks.Run();
StaffAccessChecks.Run();
SecurityOperationsChecks.Run();
LocationPrivacyChecks.Run();
PrivacyNoticeChecks.Run();
SeriousIncidentChecks.Run();
IdentityStoreChecks.Run();
ModePrivacyChecks.Run();
MediaChecks.Run();
MediaRecoveryChecks.Run();
SafetyResourceChecks.Run();
NotificationChecks.Run();
StandingChecks.Run();
ChatAttachmentChecks.Run();
await EmailOutcomeChecks.Run();
await PushProviderChecks.Run();
await ContactIdentityChecks.Run();
EmailRetirementChecks.Run();
await PhoneChecks.Run();
ConnectionChecks.Run();
MeetingPlanChecks.Run();
PrivateFeedbackChecks.Run();
AppearanceSyncChecks.Run();
await PrototypeClientChecks.Run();
await PrototypeConversationClientChecks.Run();
await NativeSignInChecks.Run();
await RegistryClientChecks.Run();
await PersistenceChecks.Run();

var expected = new FoundationStatus("Swoovr.Api", "foundation");
using var http = new HttpClient(new StatusHandler()) { BaseAddress = new Uri("https://example.invalid/") };
var actual = await new FoundationClient(http).GetStatusAsync();
if (actual != expected) throw new Exception("Typed client contract mismatch.");
using var empty = new HttpClient(new StatusHandler(empty: true)) { BaseAddress = http.BaseAddress };
try { await new FoundationClient(empty).GetStatusAsync(); throw new Exception("Expected empty response rejection."); }
catch (InvalidOperationException) { }
Console.WriteLine("PASS: typed client route, JSON contract and empty response handling.");

sealed class StatusHandler(bool empty = false) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.TryGetValues(ApiContract.Header, out var versions) || versions.Single() != ApiContract.Current) throw new Exception("Typed client did not pin API contract.");
        if (request.Method != HttpMethod.Get || request.RequestUri?.AbsolutePath != "/api/status")
            throw new Exception("Unexpected API request.");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(empty ? "null" : JsonSerializer.Serialize(new FoundationStatus("Swoovr.Api", "foundation")), Encoding.UTF8, "application/json")
        });
    }
}
