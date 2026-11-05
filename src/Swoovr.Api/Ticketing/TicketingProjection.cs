using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Swoovr.Api.Ticketing;

/// <summary>Untrusted provider content is always projected as plain text, never executable markup.</summary>
public static class TicketingProjection
{
    public static string Plain(string text,string contentType)
    {
        if(text.Length>64000)throw new ArgumentException("Support content exceeds bounds.");
        if(contentType.StartsWith("text/html",StringComparison.OrdinalIgnoreCase))
        {
            text=Regex.Replace(text,@"<(script|style|iframe|object)\b[^>]*>[\s\S]*?</\1\s*>","",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1));
            text=Regex.Replace(text,@"<[^>]*>","",RegexOptions.None,TimeSpan.FromSeconds(1));
            text=WebUtility.HtmlDecode(text);
        }
        else if(!contentType.StartsWith("text/plain",StringComparison.OrdinalIgnoreCase))return "[Unsupported message format]";
        return new string(text.Where(c=>!char.IsControl(c)||c is '\n' or '\t').ToArray()).Trim();
    }
    public static string Filename(string value)
    {
        var last=value.Replace('\\','/').Split('/').Last();
        var clean=new string(last.Where(c=>char.IsAsciiLetterOrDigit(c)||c is ' ' or '.' or '-' or '_').Take(100).ToArray());
        return string.IsNullOrWhiteSpace(clean)?"attachment.txt":clean;
    }
    public static string Reference(long id)=>id>0?$"TK-{id}":throw new ArgumentException("Invalid ticket.");
    public static void ValidateAttachment(string filename,string type,byte[] data)
    {
        // DEFAULT: ordinary UTF-8 text only. Binary formats need a configured malware/content scanning product.
        if(type!="text/plain"||!filename.EndsWith(".txt",StringComparison.OrdinalIgnoreCase)||data.Length is <1 or >65536)
            throw new ArgumentException("Only ordinary text attachments up to 64 KiB are supported.");
        var value=new UTF8Encoding(false,true).GetString(data);
        if(value.Any(c=>char.IsControl(c)&&c is not '\r' and not '\n' and not '\t')||
            Regex.IsMatch(filename+" "+value,@"passport|identity\s*document|driver.?s?\s*licen[cs]e|credit\s*card|private\s*key|BEGIN [A-Z ]*PRIVATE|sensitive\s*evidence",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1)))
            throw new ArgumentException("Sensitive evidence and identity documents cannot be sent to support.");
    }
}
