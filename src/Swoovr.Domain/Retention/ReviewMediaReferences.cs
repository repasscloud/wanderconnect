using System.Text.Json;

namespace Swoovr.Domain;

/// <summary>Legacy review snapshots: malformed/unrecognised provenance is unknown, never evidence that no object is needed.</summary>
public sealed record ReviewMediaReferences(bool Known, IReadOnlyList<Guid> Ids)
{
    public static ReviewMediaReferences Read(string kind, string snapshot)
    {
        try
        {
            using var document = JsonDocument.Parse(snapshot);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return new(false, []);
            if (kind is "profile" or "bot" or "request") return new(true, []);
            if (kind is not ("message" or "conversation") || !root.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
                return new(false, []);
            var ids = new HashSet<Guid>();
            foreach (var message in messages.EnumerateArray())
            {
                if (message.ValueKind != JsonValueKind.Object) return new(false, []);
                if (!message.TryGetProperty("photo", out var photo) || photo.ValueKind == JsonValueKind.Null) continue;
                if (photo.ValueKind != JsonValueKind.String || !Guid.TryParse(photo.GetString(), out var id) || id == Guid.Empty) return new(false, []);
                ids.Add(id);
            }
            return new(true, ids.ToList());
        }
        catch (JsonException) { return new(false, []); }
    }
}
