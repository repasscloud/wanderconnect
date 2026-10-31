using System.Security.Cryptography;
using System.Text;
namespace Swoovr.Domain;

public enum TrustedContactConsent { Pending, Confirmed, Declined, Withdrawn, Removed, Expired }
public enum MeetingSafetyStatus { Planned, Active, CheckInDue, MissedCheckIn, HelpRequested, HomeSafe, Ended, Cancelled, Expired, Revoked }
public sealed class TrustedContact
{
    public Guid Id {get;set;}
    public Guid MemberId {get;set;}
    public string DisplayName {get;set;}="";
    public string PreferredFirstName {get;set;}="";
    public string Channel {get;set;}="";
    public byte[] DestinationEncrypted {get;set;}=[];
    public byte[] DestinationLookupHash {get;set;}=[];
    public TrustedContactConsent Consent {get;set;}
    public byte[]? ConsentTokenHash {get;set;}
    public DateTimeOffset? ConsentTokenExpiresAt {get;set;}
    public DateTimeOffset? ConsentRespondedAt {get;set;}
    public byte[]? ManagementTokenHash {get;set;}
    public DateTimeOffset? ConfirmedAt {get;set;}
    public DateTimeOffset? WithdrawnAt {get;set;}
    public DateTimeOffset CreatedAt {get;set;}
    public DateTimeOffset? PurgeAfter {get;set;}
    public string InvitationStatus {get;set;}="pending";
}
public sealed class MeetingSafetySession
{
    public Guid Id {get;set;}
    public Guid MemberId {get;set;}
    public string PreferredFirstName {get;set;}="";
    public DateTimeOffset StartsAt {get;set;}
    public DateTimeOffset FinalCheckInAt {get;set;}
    public string TimeZoneId {get;set;}="";
    public string? VenueOrArea {get;set;}
    public bool SharePlace {get;set;}
    public string? MemberNote {get;set;}
    public MeetingSafetyStatus Status {get;set;}
    public DateTimeOffset? ClosedAt {get;set;}
    public DateTimeOffset? PurgeAfter {get;set;}
    public DateTimeOffset CreatedAt {get;set;}
    public DateTimeOffset UpdatedAt {get;set;}
    public DateTimeOffset? AfterPromptDismissedAt {get;set;}
    public string? PrivateFeedback {get;set;}
}
public sealed class MeetingSafetyRecipient
{
    public Guid Id {get;set;}
    public Guid SessionId {get;set;}
    public Guid TrustedContactId {get;set;}
    public DateTimeOffset CreatedAt {get;set;}
    public DateTimeOffset? RevokedAt {get;set;}
    public string NotificationStatus {get;set;}="pending";
}
/// <summary>Independent random per-recipient links; only digests persist, including delivery retries.</summary>
public sealed class MeetingSafetyLink
{
    public Guid Id {get;set;}
    public Guid RecipientId {get;set;}
    public byte[] TokenHash {get;set;}=[];
    public DateTimeOffset CreatedAt {get;set;}
    public DateTimeOffset ExpiresAt {get;set;}
    public DateTimeOffset? RevokedAt {get;set;}
}
public sealed class MeetingSafetyCheckIn
{
    public Guid Id {get;set;}
    public Guid SessionId {get;set;}
    public DateTimeOffset DueAt {get;set;}
    public DateTimeOffset GraceEndsAt {get;set;}
    public DateTimeOffset? RespondedAt {get;set;}
    public string? Response {get;set;}
    public DateTimeOffset? EscalatedAt {get;set;}
}
public sealed class MeetingSafetyEvent
{
    public long Id {get;set;}
    public Guid SessionId {get;set;}
    public string Kind {get;set;}="";
    public DateTimeOffset At {get;set;}
    public Guid? CheckInId {get;set;}
    public Guid? CommandId {get;set;}
}
/// <summary>Minimal durable intents/outcomes; never destinations, bearer tokens or message bodies.</summary>
public sealed class MeetingSafetyOutbox
{
    public Guid Id {get;set;}
    public Guid MemberId {get;set;}
    public Guid? ContactId {get;set;}
    public Guid? SessionId {get;set;}
    public Guid? CheckInId {get;set;}
    public string Kind {get;set;}="";
    public string DedupeKey {get;set;}="";
    public string Status {get;set;}="pending";
    public DateTimeOffset CreatedAt {get;set;}
    public DateTimeOffset NextAttemptAt {get;set;}
    public DateTimeOffset? AttemptedAt {get;set;}
    public DateTimeOffset? CompletedAt {get;set;}
    public int Attempts {get;set;}
}
public static class MeetingSafetyPolicy
{
    public const int ContactsMax=3;
    public static string Token()=>Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
    public static byte[] TokenHash(string token)=>SHA256.HashData(Encoding.UTF8.GetBytes(token));
    public static bool TokenShape(string? token)=>token is{Length:43}&&token.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_');
    public static string? Destination(string channel,string value)=>channel switch
    {
        "email"=>EmailAddress.Normalise(value),
        "sms" when value.StartsWith('+')&&value.Length is>=9 and<=16&&value[1]!='0'&&value[1..].All(char.IsAsciiDigit)=>value,
        _=>null
    };
    public static bool ValidWindow(DateTimeOffset start,DateTimeOffset end,string zone,DateTimeOffset now)
    {
        if(string.IsNullOrWhiteSpace(zone))return false;
        if(start.Offset!=TimeSpan.Zero||end.Offset!=TimeSpan.Zero||start<now.AddDays(-1)||start>now.AddDays(30)||end<=start||end<=now||end>start.AddDays(30))return false;
        try{_ = TimeZoneInfo.FindSystemTimeZoneById(zone);return true;}catch(TimeZoneNotFoundException){return false;}catch(InvalidTimeZoneException){return false;}
    }
    public static DateTimeOffset TokenExpiry(MeetingSafetySession session,DateTimeOffset created)
    {
        var end=((session.Status is MeetingSafetyStatus.HomeSafe or MeetingSafetyStatus.Ended or MeetingSafetyStatus.Cancelled ? session.ClosedAt : null)??session.FinalCheckInAt).AddHours(24);var cap=created.AddDays(30);return end<cap?end:cap;
    }
    public static bool AfterPrompt(MeetingSafetySession session,DateTimeOffset now)=>session.AfterPromptDismissedAt==null&&now>=session.FinalCheckInAt.AddHours(2)&&session.Status is not(MeetingSafetyStatus.Cancelled or MeetingSafetyStatus.Revoked or MeetingSafetyStatus.HelpRequested or MeetingSafetyStatus.MissedCheckIn);
    public static bool Closed(MeetingSafetyStatus status)=>status is MeetingSafetyStatus.HomeSafe or MeetingSafetyStatus.Ended or MeetingSafetyStatus.Cancelled or MeetingSafetyStatus.Expired or MeetingSafetyStatus.Revoked;
    public static bool Missed(MeetingSafetyCheckIn item,DateTimeOffset now)=>item.RespondedAt==null&&item.EscalatedAt==null&&now>=item.GraceEndsAt;
    public static string State(MeetingSafetyStatus status)=>status switch{MeetingSafetyStatus.CheckInDue=>"check_in_due",MeetingSafetyStatus.MissedCheckIn=>"missed_check_in",MeetingSafetyStatus.HelpRequested=>"help_requested",MeetingSafetyStatus.HomeSafe=>"home_safe",_=>status.ToString().ToLowerInvariant()};
}
