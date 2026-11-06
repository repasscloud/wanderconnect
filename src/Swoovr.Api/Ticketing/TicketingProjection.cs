using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Swoovr.Domain;

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
    /// <summary>
    /// Issue 220 (D330): a member or staff attachment checked against the owner's list and limits
    /// (SupportAttachmentPolicy). The type comes from the extension and the content must match it (file signatures, or
    /// valid text for text and CSV), so a renamed program or page is refused. Text keeps the sensitive-evidence screen.
    /// Returns the clean filename and the content type to store. OPEN: malware scanning before production (Q135).
    /// </summary>
    public static SupportUpload MemberAttachment(string filename,byte[] data)
    {
        var name=Filename(filename);
        if(Swoovr.Contracts.SupportAttachmentPolicy.TypeOf(name) is not { } type||data.Length<1||data.Length>Swoovr.Contracts.SupportAttachmentPolicy.MaxBytes(type.Kind))
            throw new ArgumentException("That file type or size isn't accepted.");
        if(!ContentMatches(Swoovr.Contracts.SupportAttachmentPolicy.Extension(name),data))throw new ArgumentException("The file's content doesn't match its type.");
        if(type.Mime.StartsWith("text/",StringComparison.Ordinal))
        {
            var value=new UTF8Encoding(false,true).GetString(data);
            if(Regex.IsMatch(name+" "+value,@"passport|identity\s*document|driver.?s?\s*licen[cs]e|credit\s*card|private\s*key|BEGIN [A-Z ]*PRIVATE|sensitive\s*evidence",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1)))
                throw new ArgumentException("Sensitive evidence and identity documents cannot be sent to support.");
        }
        return new(name,type.Mime,data);
    }

    /// <summary>The message's files together: count and total size (each file is checked by MemberAttachment).</summary>
    public static IReadOnlyList<SupportUpload> MemberAttachments(IEnumerable<(string Filename,byte[] Data)> files)
    {
        var list=files.Select(f=>MemberAttachment(f.Filename,f.Data)).ToArray();
        if(list.Length>Swoovr.Contracts.SupportAttachmentPolicy.MaxAttachmentsPerMessage||list.Sum(f=>(long)f.Data.Length)>Swoovr.Contracts.SupportAttachmentPolicy.MaxTotalMessageBytes)
            throw new ArgumentException("Too many or too large attachments.");
        return list;
    }

    private static bool ContentMatches(string extension,byte[] d)
    {
        static bool At(byte[] d,int offset,params byte[] magic)=>d.Length>=offset+magic.Length&&d.AsSpan(offset,magic.Length).SequenceEqual(magic);
        static bool Ascii(byte[] d,int offset,string text)=>At(d,offset,Encoding.ASCII.GetBytes(text));
        static string Brand(byte[] d)=>d.Length>=12&&Ascii(d,4,"ftyp")?Encoding.ASCII.GetString(d,8,4):"";
        byte[] ole=[0xD0,0xCF,0x11,0xE0,0xA1,0xB1,0x1A,0xE1];
        return extension switch
        {
            ".jpg" or ".jpeg"=>At(d,0,0xFF,0xD8,0xFF),
            ".png"=>At(d,0,0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A),
            ".gif"=>Ascii(d,0,"GIF87a")||Ascii(d,0,"GIF89a"),
            ".webp"=>Ascii(d,0,"RIFF")&&Ascii(d,8,"WEBP"),
            ".heic" or ".heif"=>Brand(d) is "heic" or "heix" or "hevc" or "hevx" or "heim" or "heis" or "mif1" or "msf1",
            ".pdf"=>Ascii(d,0,"%PDF-"),
            ".doc" or ".xls"=>At(d,0,ole),
            // Office Open XML is a ZIP holding [Content_Types].xml.
            ".docx" or ".xlsx"=>At(d,0,0x50,0x4B,0x03,0x04)&&d.AsSpan().IndexOf("[Content_Types].xml"u8)>=0,
            ".mp4" or ".m4a"=>Brand(d).Length==4,
            ".mov"=>Brand(d)=="qt  "||Ascii(d,4,"moov")||Ascii(d,4,"wide")||Ascii(d,4,"mdat"),
            ".webm"=>At(d,0,0x1A,0x45,0xDF,0xA3),
            ".mp3"=>Ascii(d,0,"ID3")||d.Length>1&&d[0]==0xFF&&(d[1]&0xE0)==0xE0,
            ".wav"=>Ascii(d,0,"RIFF")&&Ascii(d,8,"WAVE"),
            ".txt" or ".log" or ".md" or ".csv"=>Text(d),
            _=>false,
        };
    }

    /// <summary>Valid UTF-8 without control characters other than tabs and line breaks (a BOM is allowed).</summary>
    private static bool Text(byte[] data)
    {
        try
        {
            var value=new UTF8Encoding(false,true).GetString(data).TrimStart('﻿');
            return !value.Any(c=>char.IsControl(c)&&c is not '\r' and not '\n' and not '\t');
        }
        catch(DecoderFallbackException){return false;}
    }

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
