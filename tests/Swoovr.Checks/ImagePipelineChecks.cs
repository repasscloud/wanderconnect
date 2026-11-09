using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Swoovr.Api.Media;

static class ImagePipelineChecks
{
    public static void Run()
    {
        var checks=0;
        void Check(bool value,string message){if(!value)throw new Exception("Image pipeline: "+message);checks++;}
        var disabled=ImagePipelineOptions.Read(new ConfigurationBuilder().Build(),true);
        Check(!disabled.Enabled,"disabled by default");
        var values=new Dictionary<string,string?> { ["Media:Pipeline:Enabled"]="true", ["Media:Pipeline:ServiceToken"]=new string('s',32), ["Media:Pipeline:UploadSigningKey"]=new string('u',32), ["Media:Pipeline:UploadOrigin"]="https://images-upload-uat.swoovr.app", ["Media:Pipeline:AcceptedPolicyVersion"]="swoovr-images-2026-11-08-v1" };
        var options=ImagePipelineOptions.Read(new ConfigurationBuilder().AddInMemoryCollection(values).Build(),false);
        Check(options.Enabled&&!options.Deliverable,"enabled foundation cannot publish");
        Check(options.UploadOrigin?.AbsoluteUri=="https://images-upload-uat.swoovr.app/"&&options.PolicyVersion=="swoovr-images-2026-11-08-v1","owner UAT upload origin and exact policy accepted without enabling delivery");
        values["Media:Pipeline:UploadSigningKey"]=values["Media:Pipeline:ServiceToken"];
        try{ImagePipelineOptions.Read(new ConfigurationBuilder().AddInMemoryCollection(values).Build(),false);Check(false,"shared credentials refused");}catch(InvalidOperationException){checks++;}
        values["Media:Pipeline:UploadSigningKey"]=new string('u',32);
        Check(options.Authenticates("Bearer "+new string('s',32)),"service bearer accepted");
        Check(!options.Authenticates("Bearer "+new string('x',32))&&!options.Authenticates(""),"wrong/missing bearer denied");
        var now=DateTimeOffset.Parse("2026-11-08T12:00:00Z");
        var capability=new ImageUploadCapability(1,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"", "profile",10485760,now.AddMinutes(10).ToUnixTimeSeconds(),1);
        capability=capability with{SourceKey=$"incoming/{capability.ImageId:D}/{capability.UploadId:D}"};
        var token=options.Sign(capability);var parts=token.Split('.');
        var signature=HMACSHA256.HashData(Encoding.UTF8.GetBytes(new string('u',32)),Encoding.ASCII.GetBytes(parts[0]));
        Check(parts[1]==ImagePipelineOptions.Base64Url(signature),"worker-compatible HMAC over encoded payload");
        var payload=JsonDocument.Parse(Convert.FromBase64String(parts[0].Replace('-','+').Replace('_','/').PadRight((parts[0].Length+3)/4*4,'='))).RootElement;
        Check(payload.GetProperty("imageId").GetGuid()==capability.ImageId&&payload.GetProperty("expiresAt").GetInt64()==capability.ExpiresAt,"camelCase capability and Unix expiry");
        Check(ImagePipelineProtocol.ValidSourceKey(capability.SourceKey)&&!ImagePipelineProtocol.ValidSourceKey("incoming/../else"),"immutable source key grammar");
        Check(ImagePipelineProtocol.ValidHash(new string('a',64))&&!ImagePipelineProtocol.ValidHash(new string('A',64)),"canonical SHA256");
        using var left=JsonDocument.Parse("{\"b\":2,\"a\":1}");using var right=JsonDocument.Parse("{\"a\":1,\"b\":2}");
        Check(ImagePipelineProtocol.Digest(left.RootElement)==ImagePipelineProtocol.Digest(right.RootElement),"canonical callback digest ignores property order");
        values["Media:Pipeline:ServiceToken"]="short";
        try{ImagePipelineOptions.Read(new ConfigurationBuilder().AddInMemoryCollection(values).Build(),false);Check(false,"short credential refused");}catch(InvalidOperationException){checks++;}
        values["Media:Pipeline:ServiceToken"]=new string('s',32);values["Media:Pipeline:UploadOrigin"]="http://upload.example.test";
        try{ImagePipelineOptions.Read(new ConfigurationBuilder().AddInMemoryCollection(values).Build(),false);Check(false,"hosted insecure origin refused");}catch(InvalidOperationException){checks++;}
        Console.WriteLine($"PASS: image pipeline foundation ({checks} checks; staging-only, strict auth/capabilities/canonical receipts).");
    }
}
