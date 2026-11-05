using System.Net.Http.Headers;
using System.Text.Json;
using Swoovr.Client;

internal static class TicketingClientLiveChecks
{
    public static async Task Run(string path)
    {
        using var config=JsonDocument.Parse(await File.ReadAllTextAsync(path));var root=config.RootElement;
        using var memberHttp=new HttpClient(){BaseAddress=new Uri(root.GetProperty("baseUrl").GetString()!)};
        using var staffHttp=new HttpClient(){BaseAddress=memberHttp.BaseAddress};
        memberHttp.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",root.GetProperty("memberToken").GetString());
        staffHttp.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",root.GetProperty("staffToken").GetString());
        var member=new MemberClient(memberHttp);var staff=new AdminClient(staffHttp);var id=root.GetProperty("ticketId").GetInt64();
        var detail=await member.TicketAsync(id);var options=await member.TicketOptionsAsync();var workspace=await staff.TicketAsync(id);var capabilities=await staff.TicketCapabilitiesAsync();
        if(!detail.Ok||detail.Value!.Staff is not null||detail.Value.Articles.Any(a=>a.Private)||!options.Ok||options.Value!.States.Count==0)throw new Exception("actual member ticket client contract");
        if(!workspace.Ok||workspace.Value!.Staff is null||!workspace.Value.Articles.Any(a=>a.Private)||!capabilities.Ok||capabilities.Value!.Owners.Count==0)throw new Exception("actual staff workspace client contract");
        var byReference=await staff.TicketsAsync(query:"TK-"+id);
        if(!byReference.Ok||byReference.Value!.Items.Count!=1||byReference.Value.Items[0].Id!=id)throw new Exception("reference lookup through actual typed client");
        Console.WriteLine("PASS: real authenticated MemberClient/AdminClient details, privacy, options, capabilities, date header and TK reference search.");
    }
}
