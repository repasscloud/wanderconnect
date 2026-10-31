using System.Net.Http.Json;
using System.Text.Json;
using Swoovr.Contracts;
namespace Swoovr.Client;

public sealed record AppearanceResponse(int Status,AppearanceSnapshot? Value,string? Code,string? Owner=null)
{public bool Ok=>Status is >=200 and <300&&Value is not null;}
public sealed partial class MemberClient
{
    /// <summary>204 is a known absent preference. An optional owner binds a cache refresh to its original session.</summary>
    public async Task<AppearanceResponse> AppearanceAsync(string? expectedOwner=null,CancellationToken token=default)
    {
        if(expectedOwner is not null&&!AppearanceOwner.Valid(expectedOwner))return new(400,null,"appearance-owner-required");
        using var request=new HttpRequestMessage(HttpMethod.Get,"api/me/appearance");if(expectedOwner is not null)request.Headers.Add(AppearanceOwner.Header,expectedOwner);
        using var response=await http.SendAsync(request,token);
        if(response.IsSuccessStatusCode)
        {
            var owner=ReadAppearanceOwner(response,expectedOwner);if(owner is null)return new(502,null,"invalid-appearance-response");
            if((int)response.StatusCode==204)return new(204,null,null,owner);
            return await AppearanceResponseAsync(response,owner,token);
        }
        return new((int)response.StatusCode,null,await ProblemCodeAsync(response,token));
    }
    /// <summary>409 may carry the current snapshot, but Ok remains false. Never rebind a queued edit to another owner.</summary>
    public async Task<AppearanceResponse> SaveAppearanceAsync(AppearanceUpdate update,string expectedOwner,CancellationToken token=default)
    {
        if(!AppearanceOwner.Valid(expectedOwner))return new(400,null,"appearance-owner-required");
        using var request=new HttpRequestMessage(HttpMethod.Put,"api/me/appearance"){Content=JsonContent.Create(update)};request.Headers.Add(AppearanceOwner.Header,expectedOwner);
        using var response=await http.SendAsync(request,token);
        if(response.IsSuccessStatusCode)
        {
            var owner=ReadAppearanceOwner(response,expectedOwner);return owner is null?new(502,null,"invalid-appearance-response"):await AppearanceResponseAsync(response,owner,token);
        }
        if((int)response.StatusCode==409)
        {
            try
            {
                var conflict=await response.Content.ReadFromJsonAsync<AppearanceConflict>(token);
                if(conflict is{Code:"appearance-account-changed",Current:null})return new(409,null,conflict.Code);
                if(conflict?.Code is "appearance-conflict" or "appearance-revision-exhausted"
                    &&ReadAppearanceOwner(response,expectedOwner) is{}owner&&(conflict.Current is null||ValidAppearanceSnapshot(conflict.Current)))return new(409,conflict.Current,conflict.Code,owner);
            }
            catch(JsonException){}
            return new(502,null,"invalid-appearance-response");
        }
        return new((int)response.StatusCode,null,await ProblemCodeAsync(response,token));
    }
    private static string? ReadAppearanceOwner(HttpResponseMessage response,string? expected)
    {
        var values=response.Headers.TryGetValues(AppearanceOwner.Header,out var headers)?headers.Take(2).ToArray():[];
        return values is{Length:1}&&AppearanceOwner.Valid(values[0])&&(expected is null||values[0]==expected)?values[0]:null;
    }
    private static bool ValidAppearanceSnapshot(AppearanceSnapshot? value)=>value is{Revision:>0,Preference:not null};
    private static async Task<AppearanceResponse> AppearanceResponseAsync(HttpResponseMessage response,string owner,CancellationToken token)
    {
        try{var snapshot=await response.Content.ReadFromJsonAsync<AppearanceSnapshot>(token);if(ValidAppearanceSnapshot(snapshot))return new((int)response.StatusCode,snapshot,null,owner);}
        catch(JsonException){}
        return new(502,null,"invalid-appearance-response");
    }
}
