using System.Globalization;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.Devices.Sensors;
using Swoovr.Client;
using Swoovr.Contracts;
namespace Swoovr.Mobile;
public sealed class MeetingSafetyPage:ContentPage
{
    private readonly VerticalStackLayout body=new(){Padding=new Thickness(24,30),Spacing=16};
    private readonly Label status=Ui.Text(null,id:"MeetingSafetyStatus");
    private readonly HashSet<Guid> selected=[];private readonly List<string> extras=[];private readonly Dictionary<Guid,string> feedback=[];
    private string firstName="",contactName="",channel="email",destination="",zone=TimeZoneInfo.Local.Id,place="",note="";
    private DateTime startDate=DateTime.Now.AddHours(1).Date,endDate=DateTime.Now.AddHours(3).Date;private TimeSpan startTime=DateTime.Now.AddHours(1).TimeOfDay,endTime=DateTime.Now.AddHours(3).TimeOfDay;
    private bool sharePlace,includeLocation;private MeetingSafetyDashboard? data;private SafetyPack? pack;private string? country;
    public MeetingSafetyPage(){Title="Meeting safety";Content=new ScrollView{Content=body};}
    protected override async void OnAppearing(){base.OnAppearing();await GuardAsync(LoadAsync);}
    private async Task LoadAsync()
    {
        body.Children.Clear();body.Children.Add(Ui.Text("I'm meeting someone","PageTitle","MeetingSafetyTitle"));body.Children.Add(Ui.Text(MeetingSafetyText.Disclaimer,"FooterClaims"));body.Children.Add(status);
        body.Children.Add(Ui.Text("Before meeting","SectionHeading"));foreach(var advice in SafetyAdvice.BeforeMeeting)body.Children.Add(Ui.Text(advice));
        if(!await Ui.SignedInAsync()){body.Children.Add(Ui.Text("Sign in to manage voluntary meeting safety.",id:"MeetingSafetySignedOut"));return;}
        var result=await Ui.Members!.MeetingSafetyAsync();data=result.Value;if(data is null){status.Text=MeetingSafetyText.Problem(result.Code);return;}
        selected.RemoveWhere(id=>!data.Contacts.Any(c=>c.Id==id&&c.Consent=="confirmed"));
        if(data.DeliveryMode=="local-simulation-only")body.Children.Add(Ui.Text("Local prototype: email goes to the local sink; SMS and push are simulated. No external contact/device delivery is verified.","FooterClaims","MeetingSafetySimulated"));
        body.Children.Add(SafeButton("Refresh status","MeetingSafetyRefresh",LoadAsync,primary:false));
        var name=Entry(firstName,"Your preferred first name",50,"MeetingFirstName",v=>firstName=v);body.Children.Add(Ui.Card(Ui.Labelled("Your preferred first name (shared with chosen contacts)",name)));
        var contact=Entry(contactName,"Contact name",80,"TrustedContactName",v=>contactName=v);var target=Entry(destination,"Email or international mobile (+ country code)",254,"TrustedContactDestination",v=>destination=v);
        var via=new Picker{Title="Channel",ItemsSource=new[]{"Email","SMS (when configured; simulated locally)"},SelectedIndex=channel=="sms"?1:0,AutomationId="TrustedContactChannel"};via.SelectedIndexChanged+=(_,_)=>channel=via.SelectedIndex==1?"sms":"email";
        body.Children.Add(Ui.Card(Ui.Text("Trusted contacts","SectionHeading"),Ui.Text("Up to three active contacts. Each must agree before receiving alerts. Changing a destination requires removal and a fresh invitation. Never used for marketing.","FooterClaims"),Ui.Labelled("Contact name",contact),via,Ui.Labelled("Destination",target),SafeButton("Invite trusted contact","TrustedContactInvite",async()=>{var saved=await Ui.Members.AddTrustedContactAsync(new(contactName,channel,destination,firstName));status.Text=saved.Ok?"Invitation queued; wait for confirmed consent. Delivery is not confirmed.":MeetingSafetyText.Problem(saved.Code);if(saved.Ok){contactName="";destination="";}await LoadAsync();})));
        foreach(var item in data.Contacts)
        {
            var rows=new List<View>{Ui.Text(item.DisplayName,"TileName"),Ui.Text($"{item.DestinationHint} · {item.Consent}"),Ui.Text(MeetingSafetyText.Delivery(item.InvitationStatus),"FooterClaims")};
            if(item.Consent=="pending")rows.Add(SafeButton("Resend invitation","TrustedResend_"+item.Id,async()=>{var sent=await Ui.Members.ResendTrustedContactAsync(item.Id);status.Text=sent.Ok?"Resend queued; delivery unconfirmed.":MeetingSafetyText.Problem(sent.Code);await LoadAsync();},primary:false));
            rows.Add(SafeButton("Remove contact","TrustedRemove_"+item.Id,async()=>{var removed=await Ui.Members.RemoveTrustedContactAsync(item.Id);status.Text=removed.Ok?"Contact removed; their links are revoked.":MeetingSafetyText.Problem(removed.Code);await LoadAsync();},primary:false));body.Children.Add(Ui.Card([..rows]));
        }
        var starts=new DatePicker{Date=startDate,AutomationId="MeetingStartDate"};starts.DateSelected+=(_,_)=>startDate=starts.Date??DateTime.Today;
        var ends=new DatePicker{Date=endDate,AutomationId="MeetingEndDate"};ends.DateSelected+=(_,_)=>endDate=ends.Date??DateTime.Today;
        var startClock=new TimePicker{Time=startTime,AutomationId="MeetingStartTime"};startClock.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(TimePicker.Time))startTime=startClock.Time??TimeSpan.Zero;};
        var endClock=new TimePicker{Time=endTime,AutomationId="MeetingEndTime"};endClock.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(TimePicker.Time))endTime=endClock.Time??TimeSpan.Zero;};
        var timezone=Entry(zone,"IANA time zone, e.g. Australia/Adelaide",100,"MeetingTimeZone",v=>zone=v);
        var venue=Entry(place,"Public venue or general area; never a private address",160,"MeetingPlace",v=>place=v);
        var privateNote=new Editor{Text=note,MaxLength=500,AutoSize=EditorAutoSizeOption.TextChanges,AutomationId="MeetingPrivateNote"};privateNote.TextChanged+=(_,_)=>note=privateNote.Text??"";
        var share=new CheckBox{IsChecked=sharePlace,AutomationId="MeetingSharePlace"};share.CheckedChanged+=(_,e)=>sharePlace=e.Value;
        var form=new List<View>{Ui.Text("New standalone safety session","SectionHeading"),Ui.Text("No Swoovr date plan or QR confirmation is needed. Each check-in has a 30-minute grace period; missed check-ins alert selected confirmed contacts only.","FooterClaims"),Ui.Labelled("Start date in selected zone",starts),startClock,Ui.Labelled("Final check-in / expected end",ends),endClock,Ui.Labelled("Meeting time zone",timezone),Ui.Labelled("Public venue / general area",venue),Row(share,"Share this public venue or area with selected contacts"),Ui.Labelled("Private note (not shared)",privateNote)};
        for(var i=0;i<extras.Count;i++){var at=i;form.Add(Ui.Labelled("Additional check-in local time (yyyy-MM-ddTHH:mm)",Entry(extras[at],"yyyy-MM-ddTHH:mm",16,"MeetingExtra_"+at,v=>extras[at]=v)));}
        if(extras.Count<10)form.Add(SafeButton("Add optional check-in","MeetingAddCheckIn",async()=>{extras.Add(endDate.Add(endTime).ToString("yyyy-MM-ddTHH:mm",CultureInfo.InvariantCulture));await LoadAsync();},primary:false));
        form.Add(Ui.Text("Choose one to three confirmed contacts","TileName"));
        foreach(var item in data.Contacts.Where(c=>c.Consent=="confirmed")){var box=new CheckBox{IsChecked=selected.Contains(item.Id),AutomationId="MeetingChoose_"+item.Id};box.CheckedChanged+=(_,e)=>{if(e.Value)selected.Add(item.Id);else selected.Remove(item.Id);};form.Add(Row(box,item.DisplayName));}
        form.Add(SafeButton("Start voluntary monitoring","MeetingStart",StartAsync));body.Children.Add(Ui.Card([..form]));
        country=SafetyText.Manual(Preferences.Default.Get<string?>(SafetyText.ChoiceKey,null));pack=(await Ui.Members.SafetyAsync(country)).Value;
        var countries=new Picker{Title="Country for emergency assistance",ItemsSource=(pack?.Countries??[]).Select(c=>c.Name).ToList(),AutomationId="MeetingEmergencyCountry"};countries.SelectedIndex=country is null?-1:pack?.Countries.ToList().FindIndex(c=>c.Code==country)??-1;
        countries.SelectedIndexChanged+=async(_,_)=>{if(pack is null||countries.SelectedIndex<0)return;Preferences.Default.Set(SafetyText.ChoiceKey,pack.Countries[countries.SelectedIndex].Code);await LoadAsync();};body.Children.Add(Ui.Card(Ui.Text("Emergency assistance","SectionHeading"),countries,Ui.Text("The emergency button opens the dialler only. Swoovr never automatically places a call.","FooterClaims")));
        foreach(var session in data.Sessions)body.Children.Add(await SessionCard(session));
    }
    private async Task<View> SessionCard(MeetingSafetySessionView session)
    {
        var rows=new List<View>{Ui.Text(MeetingSafetyText.Status(session.Status),"SectionHeading","MeetingState_"+session.Id),Ui.Text(MeetingSafetyText.Time(session.StartsAt,session.TimeZoneId)+" → "+MeetingSafetyText.Time(session.FinalCheckInAt,session.TimeZoneId))};
        foreach(var recipient in session.Recipients)rows.Add(Ui.Text($"{recipient.DisplayName} · {recipient.Consent} · {MeetingSafetyText.Delivery(recipient.NotificationStatus)}","FooterClaims"));
        foreach(var check in session.CheckIns){rows.Add(Ui.Text("Check-in: "+MeetingSafetyText.Time(check.DueAt,session.TimeZoneId)+". Grace ends: "+MeetingSafetyText.Time(check.GraceEndsAt,session.TimeZoneId)+". "+(check.Response??(check.EscalatedAt is not null?"Missed / unconfirmed":"Pending"))));if(check.RespondedAt==null&&session.ClosedAt==null){if(check.DueAt<=DateTimeOffset.UtcNow)rows.Add(SafeButton("I'm OK","MeetingOK_"+check.Id,()=>ActAsync(session.Id,"ok",check.Id)));rows.Add(SafeButton("Extend by 30 minutes","MeetingExtend_"+check.Id,()=>ActAsync(session.Id,"extend",check.Id),primary:false));}}
        if(session.ClosedAt==null)
        {
            rows.Add(SafeButton("I'm home safe","MeetingHomeSafe_"+session.Id,()=>ActAsync(session.Id,"home-safe")));rows.Add(SafeButton("End meeting","MeetingEnd_"+session.Id,()=>ActAsync(session.Id,"end"),primary:false));rows.Add(SafeButton("Cancel meeting","MeetingCancel_"+session.Id,()=>ActAsync(session.Id,"cancel"),primary:false));rows.Add(SafeButton("Need help: notify selected contacts","MeetingHelp_"+session.Id,()=>ActAsync(session.Id,"help"),primary:false));
            var emergency=country is not null&&pack is{Readiness:"ready",Stale:false}?pack.Resources.FirstOrDefault(r=>r.Category=="emergency"&&r.Phone!=null&&r.LastVerifiedOn!=null):null;
            if(emergency is{Phone:{}number})rows.Add(SafeButton("Open emergency dialler — "+emergency.PhoneDisplay,"MeetingEmergency_"+session.Id,()=>{if(PhoneDialer.Default.IsSupported)PhoneDialer.Default.Open(number);else status.Text="A dialler is unavailable on this device. Use a phone to call local emergency services.";return Task.CompletedTask;}));else rows.Add(Ui.Text("Choose your country above. No current verified emergency number is selected.","FooterClaims"));
            var location=new CheckBox{IsChecked=includeLocation,AutomationId="MeetingDeviceLocation_"+session.Id};location.CheckedChanged+=(_,e)=>includeLocation=e.Value;rows.Add(Row(location,"Optional location link in device SMS only; no meeting coordinates go to Swoovr"));
            var sms=false;foreach(var recipient in session.Recipients.Where(r=>!r.Revoked&&r.Consent=="confirmed")){if(data!.Contacts.FirstOrDefault(c=>c.Id==recipient.ContactId)?.Channel!="sms")continue;sms=true;rows.Add(SafeButton("Alert "+recipient.DisplayName+" using my SMS composer","MeetingCompose_"+recipient.ContactId,()=>ComposeAsync(session,recipient.ContactId),primary:false));}
            if(!sms)rows.Add(Ui.Text("The device SMS composer needs a confirmed SMS contact. Selected email contacts still receive eligible server alerts.","FooterClaims"));rows.Add(Ui.Text(MeetingSafetyText.Composer,"FooterClaims"));
        }
        if(session.ShowAfterPrompt)
        {
            rows.Add(Ui.Text("How did it go? (optional)","TileName"));var entry=new Editor{Text=feedback.GetValueOrDefault(session.Id),MaxLength=1000,AutoSize=EditorAutoSizeOption.TextChanges,AutomationId="MeetingFeedback_"+session.Id};entry.TextChanged+=(_,_)=>feedback[session.Id]=entry.Text??"";rows.Add(Ui.Labelled("Private feedback (your member view)",entry));rows.Add(SafeButton("Save private feedback","MeetingSaveFeedback_"+session.Id,()=>ActAsync(session.Id,"feedback"),primary:false));rows.Add(SafeButton("Report an incident","MeetingReport_"+session.Id,()=>Ui.PushAsync(new SafetyPage(meetingContext:session.Id)),primary:false));rows.Add(SafeButton("Dismiss","MeetingDismiss_"+session.Id,()=>ActAsync(session.Id,"dismiss"),primary:false));rows.Add(Ui.Text("Feedback does not affect anyone's reputation. Reporting is a separate explicit action and does not automatically notify the other person.","FooterClaims"));
        }
        rows.Add(SafeButton("Report an incident from this meeting","MeetingReportAlways_"+session.Id,()=>Ui.PushAsync(new SafetyPage(meetingContext:session.Id)),primary:false));
        if(session.PrivateFeedback is{}saved)rows.Add(Ui.Text("Your private feedback: "+saved));await Task.CompletedTask;return Ui.Card([..rows]);
    }
    private async Task StartAsync()
    {
        if(!MeetingSafetyText.TryUtc(startDate.Add(startTime).ToString("yyyy-MM-ddTHH:mm",CultureInfo.InvariantCulture),zone,out var start)||!MeetingSafetyText.TryUtc(endDate.Add(endTime).ToString("yyyy-MM-ddTHH:mm",CultureInfo.InvariantCulture),zone,out var end)){status.Text="Check the zone and local times. Daylight-saving skipped/repeated times need a different clear time.";return;}
        var extra=new List<DateTimeOffset>();foreach(var value in extras){if(!MeetingSafetyText.TryUtc(value,zone,out var time)){status.Text="Check additional check-in times.";return;}extra.Add(time);}
        var result=await Ui.Members!.StartMeetingSafetyAsync(new(firstName,start,end,zone,place,sharePlace,note,selected.ToList(),extra));status.Text=result.Ok?"Voluntary monitoring scheduled. Alerts are not guaranteed to arrive.":MeetingSafetyText.Problem(result.Code);await LoadAsync();
    }
    private async Task ActAsync(Guid session,string action,Guid? check=null){var result=await Ui.Members!.MeetingSafetyActionAsync(session,new(Guid.NewGuid(),action,check,action=="feedback"?feedback.GetValueOrDefault(session):null));status.Text=result.Ok?"Meeting safety updated.":MeetingSafetyText.Problem(result.Code);await LoadAsync();}
    private async Task ComposeAsync(MeetingSafetySessionView session,Guid contact)
    {
        var target=(await Ui.Members!.TrustedContactComposeAsync(contact)).Value;if(target is not{Channel:"sms"}){status.Text="This contact has no confirmed SMS destination.";return;}
        var text="I'm meeting someone. Meeting: "+MeetingSafetyText.Time(session.StartsAt,session.TimeZoneId)+". Expected check-in: "+MeetingSafetyText.Time(session.FinalCheckInAt,session.TimeZoneId)+". Please contact me directly if concerned.";
        try
        {
            if(includeLocation){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));var location=await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium,TimeSpan.FromSeconds(10)),timeout.Token);if(location is null){status.Text="Location unavailable. Retry without a location link; no SMS is confirmed sent.";return;}text+="\nOptional device-provided location (not live tracking): https://maps.google.com/?q="+Uri.EscapeDataString(location.Latitude.ToString("R",CultureInfo.InvariantCulture)+","+location.Longitude.ToString("R",CultureInfo.InvariantCulture));}
            if(!Sms.Default.IsComposeSupported){status.Text="An SMS composer is unavailable on this device; no message is confirmed sent.";return;}
            await Sms.Default.ComposeAsync(new SmsMessage(text,new[]{target.Destination}));status.Text=MeetingSafetyText.Composer;
        }
        catch(Exception error)when(error is FeatureNotSupportedException or FeatureNotEnabledException or PermissionException or OperationCanceledException){status.Text="The composer or optional location was unavailable. No message is confirmed sent.";}
    }
    private Button SafeButton(string text,string id,Func<Task> action,bool primary=true)=>Ui.Button(text,id,()=>GuardAsync(action),primary);
    private async Task GuardAsync(Func<Task> action)
    {try{await action();}catch(Exception error)when(error is HttpRequestException or TaskCanceledException){status.Text="Action outcome unconfirmed. Refresh status before repeating; do not rely on an unconfirmed alert.";}}
    private static Entry Entry(string value,string placeholder,int max,string id,Action<string> changed){var view=new Entry{Text=value,Placeholder=placeholder,MaxLength=max,AutomationId=id};view.TextChanged+=(_,_)=>changed(view.Text??"");return view;}
    private static View Row(CheckBox box,string label)=>new HorizontalStackLayout{Spacing=8,Children={box,Ui.Text(label)}};
}
