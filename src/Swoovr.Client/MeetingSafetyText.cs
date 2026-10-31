using System.Globalization;
namespace Swoovr.Client;
public static class MeetingSafetyText
{
    public const string Disclaimer="Meeting safety is voluntary and available to all members, including Free. It does not guarantee physical safety. Swoovr does not monitor emergencies or dispatch responders. Alerts may fail or arrive late; agree with your trusted contact about what to do.";
    public const string Composer="Opening the SMS composer does not confirm that a message was sent or delivered.";
    public static string Problem(string? code)=>code switch
    {
        "meeting-safety-check-in-not-due"=>"This check-in is not due yet. You can extend it or mark home safe.",
        "trusted-contact-limit"=>"You can keep up to three active trusted contacts.",
        "trusted-contact-invitation-unavailable"=>"An invitation cannot be sent now. Ask your contact directly and try later; do not repeatedly invite someone who has declined.",
        "trusted-contact-confirmation-required"=>"Choose one to three contacts who have confirmed consent.",
        "invalid-meeting-safety-plan" or "invalid-meeting-safety-check-in"=>"Check the meeting times, time zone and confirmed contacts. End after now; additional check-ins must fall within the meeting.",
        "invalid-trusted-contact"=>"Enter a name and a valid email address or international mobile number beginning with +.",
        "meeting-safety-sms-unavailable"=>"SMS is not configured here. Use email for now.",
        _=>"Meeting safety could not complete this action. Check your connection and try again. Do not rely on an unconfirmed alert."
    };
    public static string Status(string value)=>value switch{"home_safe"=>"Member marked home safe","missed_check_in"=>"Missed / unconfirmed check-in","help_requested"=>"Help requested","check_in_due"=>"Check-in due","ended"=>"Monitoring ended; safety was not asserted","cancelled"=>"Cancelled","revoked"=>"Monitoring revoked","expired"=>"Monitoring expired",_=>value};
    public static string Delivery(string value)=>value switch{"rate-limited"=>"Not sent: send limit reached; contact directly",
        "accepted"=>"Provider accepted; delivery unconfirmed","local-sink-accepted"=>"Local mail sink accepted (not delivered externally)","simulated"=>"Simulated; no SMS or push delivered","unknown"=>"Provider acceptance unknown; not automatically retried","retry"=>"Known unsuccessful attempt; retry pending","rejected" or "failed"=>"Notification failed","centre-recorded"=>"App notification recorded; device delivery unconfirmed","suppressed"=>"No longer eligible to send",_=>value};
    public static string Time(DateTimeOffset value,string zone)
    {try{return TimeZoneInfo.ConvertTime(value,TimeZoneInfo.FindSystemTimeZoneById(zone)).ToString("d MMM yyyy HH:mm zzz",CultureInfo.InvariantCulture)+" ("+zone+")";}catch(TimeZoneNotFoundException){return value.ToString("u")+" UTC";}catch(InvalidTimeZoneException){return value.ToString("u")+" UTC";}}
    public static bool TryUtc(string local,string zone,out DateTimeOffset utc)
    {
        utc=default;if(!DateTime.TryParse(local,CultureInfo.InvariantCulture,DateTimeStyles.None,out var time))return false;
        time=DateTime.SpecifyKind(time,DateTimeKind.Unspecified);
        try{var target=TimeZoneInfo.FindSystemTimeZoneById(zone);if(target.IsInvalidTime(time)||target.IsAmbiguousTime(time))return false;utc=new(TimeZoneInfo.ConvertTimeToUtc(time,target));return true;}
        catch(TimeZoneNotFoundException){return false;}catch(InvalidTimeZoneException){return false;}
    }
}
