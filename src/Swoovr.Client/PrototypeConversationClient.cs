using System.Net.Http.Json;
using Swoovr.Contracts;

namespace Swoovr.Client;

// Fictional service only. Do not attach credentials or use for member messages.
public sealed class PrototypeConversationClient(HttpClient http)
{
    private const string Path="api/v1/prototype/conversations";
    public async Task<PrototypeConversation> CreateAsync(string travel,CancellationToken cancellationToken=default)
    {
        if(travel is not ("local" or "upcoming" or "arrived"))throw new ArgumentException("Unsupported fictional presence.");
        using var response=await http.PostAsJsonAsync(Path,new PrototypeConversationCreate(travel),cancellationToken);
        response.EnsureSuccessStatusCode();
        var value=Validate(await response.Content.ReadFromJsonAsync<PrototypeConversation>(cancellationToken),null,"alex");
        return value is {Revision:0,State:"None",CanRead:false,CanReport:false} ? value : throw new InvalidOperationException("Scenario unavailable.");
    }
    public async Task<PrototypeConversation> GetAsync(Guid id,string actor,CancellationToken cancellationToken=default)
    {
        ValidateInput(id,actor);
        return Validate(await http.GetFromJsonAsync<PrototypeConversation>($"{Path}/{id}?actor={actor}",cancellationToken),id,actor);
    }
    public async Task<PrototypeConversation> ActAsync(Guid id,PrototypeConversationAction action,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ValidateInput(id,action.Actor);
        if(action.Action is not ("request" or "accept" or "decline" or "send" or "close" or "block") || action.Revision<0 || action.Revision==long.MaxValue)
            throw new ArgumentException("Unsupported fictional action.");
        using var response=await http.PostAsJsonAsync($"{Path}/{id}/actions",action,cancellationToken);
        response.EnsureSuccessStatusCode();
        var value=Validate(await response.Content.ReadFromJsonAsync<PrototypeConversation>(cancellationToken),id,action.Actor);
        return value.Revision==action.Revision+1 ? value : throw new InvalidOperationException("Scenario unavailable.");
    }
    private static void ValidateInput(Guid id,string actor)
    {
        if(id==Guid.Empty || actor is not ("alex" or "jamie"))throw new ArgumentException("Unsupported fictional participant.");
    }
    private static PrototypeConversation Validate(PrototypeConversation? value,Guid? id,string actor)
    {
        if(value is not {IsSimulation:true,Messages:not null} || value.Id==Guid.Empty || (id.HasValue && value.Id!=id.Value)
            || value.Actor!=actor || value.Revision<0 || value.State is not ("None" or "Pending" or "Accepted" or "Declined" or "Closed" or "Unavailable")
            || value.Messages.Count>64 || (!value.CanRead && value.Messages.Count!=0) || (value.CanRead && (value.State is not ("Pending" or "Accepted") || value.Messages.Count==0))
            || value.Messages.Any(m=>m is null || m.Sender is not ("Alex" or "Jamie") || m.Text is null || !m.Text.StartsWith("Fictional ",StringComparison.Ordinal) || m.Text.Length>1000))
            throw new InvalidOperationException("Only bound fictional scenario data is accepted.");
        return value;
    }
}
