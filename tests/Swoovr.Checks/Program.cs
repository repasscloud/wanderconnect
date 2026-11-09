using System.Net;
using System.Text;
using Swoovr.Client;
using Swoovr.Contracts;
using System.Text.Json;

if(args is ["--staff-management-checks"]){MfaChecks.Run();StaffManagementChecks.Run();await IdentityDirectoryChecks.Run();await PersistenceChecks.Run(staffOnly:true);return;}

if(args is ["--ticketing-lifecycle-live",var lifecyclePath]){await TicketingLifecycleLiveChecks.Run(lifecyclePath);return;}

if(args is ["--ticketing-client-live",var path]){await TicketingClientLiveChecks.Run(path);return;}

if(args is ["--ticketing-persistence-checks"]){await PersistenceChecks.Run(ticketingOnly:true);return;}

if(args is ["--zammad-live", var privateDirectory]){await ZammadLiveChecks.Run(privateDirectory);return;}

if(args is ["--ticketing-checks"]){TicketingRuntimeChecks.Run();await TicketingGuestChecks.Run();TicketingChecks.Run();TicketingWebhookChecks.Run();await ZammadTransportChecks.Run();return;}

if(args is ["--image-pipeline-persistence-checks"]){ImagePipelineChecks.Run();await ImagePipelinePersistenceChecks.Run();return;}

if(args is ["--image-pipeline-checks"]){ImagePipelineChecks.Run();return;}

if(args is ["--media-checks"]){MediaChecks.Run();ImagePipelineChecks.Run();return;}
if(args is ["--device-client-checks"]){await DeviceClientChecks.Run();return;}
if(args is ["--sms-checks"]){await SmsTransportChecks.Run();return;}

if(args is ["--hosting-checks"]){HostingPolicyChecks.Run();await HostingProxyChecks.Run();await SmsSelectionChecks.Run();return;}

if(args is ["--appearance-client-checks"]){AppearanceSyncChecks.Run();AppearanceCacheChecks.Run();await AppearanceClientChecks.Run();await AppearanceCoordinatorChecks.Run();return;}

if(args is ["--mailersend-checks"]){await MailerSendChecks.Run((name,ok)=>{if(!ok)throw new Exception(name);});return;}

if(args is ["--contact-provider-probe"])
{
    await ContactIdentityProviderProbe.Run();
    return;
}

if(args is ["--policy-checks"]){PublicPolicyChecks.Run();PolicyNoticeChecks.Run();await PersistenceChecks.Run(policiesOnly:true);return;}

TicketingRuntimeChecks.Run();await TicketingGuestChecks.Run();TicketingChecks.Run();
TicketingWebhookChecks.Run();
await ZammadTransportChecks.Run();
HostingPolicyChecks.Run();
await HostingProxyChecks.Run();
await SmsSelectionChecks.Run();
await SmsTransportChecks.Run();
await DeviceClientChecks.Run();
PublicPolicyChecks.Run();
AppVersionChecks.Run();
PolicyNoticeChecks.Run();
MeetingSafetyChecks.Run();
MeetingSafetyContactAccessChecks.Run();
ContentRemovalChecks.Run();
TextHarmChecks.Run();
Swoovr.Checks.ModerationTransparencyChecks.Run((name,ok)=>{if(!ok)throw new Exception(name);});
ReportOutcomeChecks.Run((name,ok)=>{if(!ok)throw new Exception("Report outcome: "+name);});
BannedIdentifierChecks.Run();
FallbackKeyChecks.Run();
ApiContractChecks.Run();
AccessPolicyChecks.Run();
StaffManagementChecks.Run();
MfaChecks.Run();
await IdentityDirectoryChecks.Run();
AccountPolicyChecks.Run();
AccountSearchChecks.Run();
RegistrationPolicyChecks.Run();
RegisteredNameChecks.Run();
ContactChangeEvidenceChecks.Run();
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
SessionRetirementChecks.Run();
DobCorrectionChecks.Run();
AccountInspectionChecks.Run();
DeletionChecks.Run();
LegalHoldChecks.Run();
SafetyRetentionChecks.Run();
LawEnforcementChecks.Run();
StaffAccessChecks.Run();
SecurityOperationsChecks.Run();
LocationPrivacyChecks.Run();
NearMeChecks.Run();
HomeCityCheckChecks.Run();
PrivacyNoticeChecks.Run();
HelpdeskRetentionAndScanningChecks.Run();
SeriousIncidentChecks.Run();
IdentityStoreChecks.Run();
ModePrivacyChecks.Run();
MediaChecks.Run();ImagePipelineChecks.Run();
MediaRecoveryChecks.Run();
SafetyResourceChecks.Run();
NotificationChecks.Run();
StandingChecks.Run();
ChatAttachmentChecks.Run();
PhotoAlbumChecks.Run();
await EmailOutcomeChecks.Run();
await MailerSendChecks.Run((name,ok)=>{if(!ok)throw new Exception(name);});
await PushProviderChecks.Run();
await ContactIdentityChecks.Run();
await SignInChecks.Run();
EmailRetirementChecks.Run();
await PhoneChecks.Run();
ConnectionChecks.Run();
MeetingPlanChecks.Run();
PrivateFeedbackChecks.Run();
AppearanceSyncChecks.Run();
AppearanceCacheChecks.Run();
await AppearanceClientChecks.Run();
await AppearanceCoordinatorChecks.Run();
await PrototypeClientChecks.Run();
await PrototypeConversationClientChecks.Run();
await NativeSignInChecks.Run();
await RegistryClientChecks.Run();
await StaffSessionRenewalChecks.Run();
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
