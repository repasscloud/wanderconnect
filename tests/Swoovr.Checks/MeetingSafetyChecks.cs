using Swoovr.Domain;
using System.Security.Cryptography;
using System.Text;
static class MeetingSafetyChecks
{
    public static void Run()
    {
        var n=0;void Check(bool ok){if(!ok)throw new Exception("Meeting safety policy failed.");n++;}
        var now=DateTimeOffset.UtcNow;
        var session=new MeetingSafetySession{CreatedAt=now,StartsAt=now.AddHours(1),FinalCheckInAt=now.AddHours(3),TimeZoneId="Australia/Adelaide"};
        Check(MeetingSafetyPolicy.ValidWindow(session.StartsAt,session.FinalCheckInAt,session.TimeZoneId,now));
        Check(!MeetingSafetyPolicy.ValidWindow(session.StartsAt,session.StartsAt,session.TimeZoneId,now));
        Check(!MeetingSafetyPolicy.ValidWindow(session.StartsAt,session.FinalCheckInAt,"Unknown/Zone",now));
        Check(MeetingSafetyPolicy.TokenExpiry(session,now)==session.FinalCheckInAt.AddHours(24));
        session.FinalCheckInAt=now.AddDays(40);Check(MeetingSafetyPolicy.TokenExpiry(session,now)==now.AddDays(30));
        session.Status=MeetingSafetyStatus.HomeSafe;session.ClosedAt=now.AddHours(2);Check(MeetingSafetyPolicy.TokenExpiry(session,now)==now.AddHours(26));
        session.Status=MeetingSafetyStatus.Expired;Check(MeetingSafetyPolicy.TokenExpiry(session,now)==now.AddDays(30));
        var token=MeetingSafetyPolicy.Token();Check(token.Length==43);Check(MeetingSafetyPolicy.Token()!=token);
        Check(MeetingSafetyPolicy.TokenHash(token).Length==32);Check(!Encoding.UTF8.GetString(MeetingSafetyPolicy.TokenHash(token)).Contains(token));
        Check(MeetingSafetyPolicy.Destination("email"," Fictional.Adult@Example.test ")=="fictional.adult@example.test");
        Check(MeetingSafetyPolicy.Destination("sms","+61400000001")=="+61400000001");
        Check(MeetingSafetyPolicy.Destination("sms","0400000001")==null);
        Check(MeetingSafetyPolicy.Destination("unknown","fictional@example.test")==null);
        Check(MeetingSafetyPolicy.Closed(MeetingSafetyStatus.HomeSafe));Check(MeetingSafetyPolicy.Closed(MeetingSafetyStatus.Cancelled));Check(!MeetingSafetyPolicy.Closed(MeetingSafetyStatus.MissedCheckIn));
        var due=new MeetingSafetyCheckIn{DueAt=now,GraceEndsAt=now.AddMinutes(30)};
        Check(!MeetingSafetyPolicy.Missed(due,now.AddMinutes(29)));Check(MeetingSafetyPolicy.Missed(due,now.AddMinutes(30)));
        due.RespondedAt=now.AddMinutes(31);Check(!MeetingSafetyPolicy.Missed(due,now.AddMinutes(32)));
        var before=TimeZoneInfo.ConvertTime(new DateTimeOffset(2026,10,3,16,0,0,TimeSpan.Zero),TimeZoneInfo.FindSystemTimeZoneById("Australia/Adelaide"));
        var after=TimeZoneInfo.ConvertTime(new DateTimeOffset(2026,10,3,17,0,0,TimeSpan.Zero),TimeZoneInfo.FindSystemTimeZoneById("Australia/Adelaide"));
        Check(after.Offset!=before.Offset);
        Console.WriteLine($"PASS: meeting safety policy ({n} checks; no delivery/worker proof).");
    }
}
