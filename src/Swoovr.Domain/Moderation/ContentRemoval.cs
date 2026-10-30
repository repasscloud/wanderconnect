using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Swoovr.Domain;

/// <summary>Immutable restricted evidence: metadata only outside the existing versioned EvidenceSeal envelope.</summary>
public sealed class ContentRemoval
{
    public Guid Id {get;set;}
    public Guid CaseId {get;set;}
    public Guid ReportId {get;set;}
    public Guid SubjectAccountId {get;set;}
    public Guid ActorAccountId {get;set;}
    public string Field {get;set;}="";
    public string? ModeId {get;set;}
    public Guid? MessageId {get;set;}
    public DateTimeOffset RemovedAt {get;set;}
    /// <summary>Cleared only by guarded case-content disposal; minimal decision metadata has its separate clock.</summary>
    public byte[]? SealedSnapshot {get;set;}
    public byte[] SnapshotHash {get;set;}=[];
    public DateTimeOffset? EvidenceDisposedAt {get;set;}
}

public static class ContentRemovalPolicy
{
    public const int ReasonMax=1000;
    public static string Hash(string source)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    public static bool SameSource(string source,string? expected)
    {
        if(expected is not{Length:64})return false;
        try{return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Hash(source)),Convert.FromHexString(expected));}
        catch(FormatException){return false;}
    }
    public static bool ValidTarget(string field,string? mode,Guid? message)=>field switch
    {
        "about"=>mode is null&&message is null,
        "mode-about"=>mode is not null&&ConnectionModeIds.All.Contains(mode)&&message is null,
        "message"=>mode is null&&message is{}id&&id!=Guid.Empty,
        _=>false,
    };
    // DEFAULT under D133: ordinary support queue/non-affiliated content; admins also handle admin/affiliated cases.
    public static bool Allows(AccountRole role,CaseQueue queue,bool affiliated,bool conflicted)=>!conflicted
        &&(role==AccountRole.Admin||role==AccountRole.Support&&queue==CaseQueue.Support&&!affiliated);
    public static bool ReportContainsMessage(string kind,string snapshot,Guid message)
    {
        if(kind is not("message" or "conversation"))return false;
        try
        {
            using var doc=JsonDocument.Parse(snapshot);
            return doc.RootElement.ValueKind==JsonValueKind.Object&&doc.RootElement.TryGetProperty("messages",out var rows)
                &&rows.ValueKind==JsonValueKind.Array&&rows.EnumerateArray().Any(row=>row.ValueKind==JsonValueKind.Object
                    &&row.TryGetProperty("id",out var id)&&id.ValueKind==JsonValueKind.String&&id.TryGetGuid(out var parsed)&&parsed==message);
        }
        catch(JsonException){return false;}
    }
}
