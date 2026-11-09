using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Swoovr.Api.Media;

public sealed record ImageUploadCapability(int ProtocolVersion,Guid ImageId,Guid UploadId,Guid JobId,string SourceKey,string Purpose,long MaxBytes,long ExpiresAt,long LifecycleRevision);
public sealed record ImagePipelineOptions(bool Enabled,string ServiceToken,string UploadSigningKey,Uri? UploadOrigin,string PolicyVersion)
{
    // Deliberately cannot be enabled by config until review, custody, retention and delivery adapters are implemented.
    public bool Deliverable => false;
    public static ImagePipelineOptions Read(IConfiguration configuration,bool development)
    {
        if(!configuration.GetValue("Media:Pipeline:Enabled",false))return new(false,"","",null,"");
        var service=configuration["Media:Pipeline:ServiceToken"]??"";var signing=configuration["Media:Pipeline:UploadSigningKey"]??"";
        var policy=configuration["Media:Pipeline:AcceptedPolicyVersion"]??"";
        if(service.Length<32||signing.Length<32||service==signing||policy.Length is <1 or >80||!Regex.IsMatch(policy,"^[a-z0-9][a-z0-9._-]*$"))throw new InvalidOperationException("Image pipeline requires distinct service/signing secrets and an explicit accepted policy version.");
        if(!Uri.TryCreate(configuration["Media:Pipeline:UploadOrigin"],UriKind.Absolute,out var origin)||!string.IsNullOrEmpty(origin.UserInfo)||!string.IsNullOrEmpty(origin.Query)||!string.IsNullOrEmpty(origin.Fragment)||origin.AbsolutePath!="/"||origin.Scheme!="https"&&!(development&&origin.Scheme=="http"&&origin.IsLoopback))throw new InvalidOperationException("Image pipeline requires a secure upload origin (loopback HTTP only in Development).");
        return new(true,service,signing,origin,policy);
    }
    public bool Authenticates(string? header)
    {
        if(!Enabled||header is null||!header.StartsWith("Bearer ",StringComparison.Ordinal)||header.Length>512)return false;
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(header[7..])),SHA256.HashData(Encoding.UTF8.GetBytes(ServiceToken)));
    }
    public string Sign(ImageUploadCapability capability)
    {
        if(!Enabled)throw new InvalidOperationException("Image pipeline disabled.");
        var payload=Base64Url(JsonSerializer.SerializeToUtf8Bytes(capability,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        return payload+"."+Base64Url(HMACSHA256.HashData(Encoding.UTF8.GetBytes(UploadSigningKey),Encoding.ASCII.GetBytes(payload)));
    }
    public static string Base64Url(byte[] bytes)=>Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');
}

public static partial class ImagePipelineProtocol
{
    public const int Version=1;
    public const long MaxBytes=10*1024*1024;
    public static bool ValidSourceKey(string? key)=>key is not null&&SourceKeyPattern().IsMatch(key);
    public static bool ValidHash(string? hash)=>hash is not null&&HashPattern().IsMatch(hash);
    [GeneratedRegex("^incoming/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",RegexOptions.CultureInvariant)]private static partial Regex SourceKeyPattern();
    [GeneratedRegex("^[0-9a-f]{64}$",RegexOptions.CultureInvariant)]private static partial Regex HashPattern();
    public static string Digest(JsonElement value)
    {
        using var stream=new MemoryStream();using(var writer=new Utf8JsonWriter(stream))Write(writer,value);
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }
    private static void Write(Utf8JsonWriter writer,JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Object){writer.WriteStartObject();foreach(var p in value.EnumerateObject().OrderBy(p=>p.Name,StringComparer.Ordinal)){writer.WritePropertyName(p.Name);Write(writer,p.Value);}writer.WriteEndObject();}
        else if(value.ValueKind==JsonValueKind.Array){writer.WriteStartArray();foreach(var v in value.EnumerateArray())Write(writer,v);writer.WriteEndArray();}
        else value.WriteTo(writer);
    }
}
