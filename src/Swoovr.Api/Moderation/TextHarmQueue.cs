using System.Text.Json;
using Swoovr.Api.Evidence;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Moderation;

/// <summary>No background fire-and-forget: the caller saves the source and intent in the same transaction.</summary>
public sealed class TextHarmQueue(SwoovrDbContext db,EvidenceKeys keys,TextHarmRules rules)
{
    public Guid Enqueue(Guid account,TextHarmSource source,Guid sourceId,string? mode,string? text,DateTimeOffset now)
    {
        var revision=source==TextHarmSource.Message?sourceId:Guid.NewGuid();
        if(!rules.Enabled||string.IsNullOrWhiteSpace(text))return revision;
        if(keys.CurrentVersion is not{}version)throw new InvalidOperationException("Text review requires configured evidence keys.");
        db.Set<TextHarmIntent>().Add(new(){Id=Guid.NewGuid(),AccountId=account,Source=source,SourceId=sourceId,ModeId=mode,
            Revision=revision,CreatedAt=now,SealedText=EvidenceSeal.Seal(version,keys.Encryption[version],JsonSerializer.Serialize(text))});
        return revision;
    }
}
