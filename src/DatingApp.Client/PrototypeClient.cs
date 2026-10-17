using System.Net.Http.Json;
using DatingApp.Contracts;

namespace DatingApp.Client;

// Local fictional preview only; never attach authentication headers or real account data.
public sealed class PrototypeClient(HttpClient http)
{
    public static string Query(PrototypeFilter filter)
    {
        if (filter.Travel is not ("local" or "upcoming" or "arrived") || filter.Match is not ("any" or "all"))
            throw new ArgumentException("Unsupported prototype filter.");
        return $"travel={filter.Travel}&incognito={filter.Incognito.ToString().ToLowerInvariant()}&hideSparse={filter.HideSparse.ToString().ToLowerInvariant()}&requireFriendship={filter.RequireFriendship.ToString().ToLowerInvariant()}&match={filter.Match}&requireActivity={filter.RequireActivity.ToString().ToLowerInvariant()}";
    }
    public async Task<PrototypePeople> GetPeopleAsync(PrototypeFilter filter,CancellationToken cancellationToken=default)
    {
        var result = await http.GetFromJsonAsync<PrototypePeople>("api/v1/prototype/people?"+Query(filter),cancellationToken);
        if (result is not { IsSimulation: true, People: not null } || result.People.Any(p => !p.IsSimulation))
            throw new InvalidOperationException("Only explicitly simulated preview data is accepted.");
        return result;
    }
    public async Task<PrototypePerson> GetPersonAsync(Guid id,PrototypeFilter filter,CancellationToken cancellationToken=default)
    {
        var result = await http.GetFromJsonAsync<PrototypePerson>($"api/v1/prototype/people/{id}?"+Query(filter),cancellationToken);
        return result is { IsSimulation: true } ? result : throw new InvalidOperationException("Preview unavailable.");
    }
    public async Task<PrototypeContact> CheckContactAsync(Guid id,PrototypeFilter filter,bool disclose,CancellationToken cancellationToken=default)
    {
        var result = await http.GetFromJsonAsync<PrototypeContact>($"api/v1/prototype/people/{id}/contact-eligibility?"+Query(filter)+(disclose?"&disclose=true":""),cancellationToken);
        return result is { IsSimulation: true } ? result : throw new InvalidOperationException("Preview unavailable.");
    }
}
