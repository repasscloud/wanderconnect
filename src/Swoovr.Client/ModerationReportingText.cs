using Swoovr.Contracts;
namespace Swoovr.Client;
public static class ModerationReportingText
{
    public static readonly string[] Bases=["unknown","harassment","threats-violence","sexual-abuse","child-safety","fraud-scam","hate-discrimination","privacy","other-reviewed"];
    public static string Basis(string value)=>value switch{"unknown"=>"Unknown / not assessed","other-reviewed"=>"Other policy basis explicitly reviewed",_=>value.Replace('-',' ')};
    public static ModerationReportingChoice Choice(string? basis,string? source)
    {
        if(source is null or "" or "unknown")return new(basis);
        if(source=="staff")return new(basis,"staff-review");
        var parts=source.Split(':',2);
        if(parts.Length==2&&Guid.TryParse(parts[1],out var id))return new(basis,parts[0]=="report"?"member-report":parts[0]=="text"?"local-text-rules":"invalid",id);
        return new(basis,"invalid");
    }
}
