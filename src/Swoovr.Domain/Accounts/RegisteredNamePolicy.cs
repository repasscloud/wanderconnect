using System.Globalization;
using System.Text;

namespace Swoovr.Domain;

public sealed record RegisteredNames(string FirstName,string? LastName,bool Mononym);

/// <summary>D300: self-declared names, never proof of verified identity or public-name fallback to surname.</summary>
public static class RegisteredNamePolicy
{
    public const int MaximumLength=100;
    public static string? Part(string? value)
    {
        if(string.IsNullOrWhiteSpace(value)||value.Length>MaximumLength||value.Any(char.IsControl))return null;
        string normal;try{normal=value.Trim().Normalize(NormalizationForm.FormC);}catch(ArgumentException){return null;}
        if(normal.Length==0)return null;
        bool letter=false;
        foreach(var rune in normal.EnumerateRunes())
        {
            var category=Rune.GetUnicodeCategory(rune);
            if(category is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter){letter=true;continue;}
            if(category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark or UnicodeCategory.DashPunctuation or UnicodeCategory.SpaceSeparator)continue;
            if(rune.Value is 0x20 or 0x27 or 0x2d or 0x2e or 0x2018 or 0x2019 or 0x05f3 or 0x05f4 or 0x200c or 0x200d)continue;
            return null;
        }
        return letter?normal:null;
    }
    public static RegisteredNames? Validate(string? first,string? last,bool mononym)=>Part(first)is{}given
        &&(mononym?string.IsNullOrWhiteSpace(last):Part(last)is not null)?new(given,mononym?null:Part(last),mononym):null;
    public static bool Complete(Account account)=>account.RegisteredNamesConfirmedAt is not null
        &&Validate(account.RegisteredFirstName,account.RegisteredLastName,account.RegisteredMononym)is not null;
    public static string? InitialPublicName(string? current,RegisteredNames names)
    {
        if(!string.IsNullOrWhiteSpace(current))return current;
        var limit=Math.Min(40,names.FirstName.Length);
        if(limit<names.FirstName.Length&&char.IsHighSurrogate(names.FirstName[limit-1]))limit--;
        return names.FirstName[..limit];
    }
    public static bool Matches(RegisteredNames names,string? first,string? last)=>Part(first)is{}given
        &&string.Equals(names.FirstName,given,StringComparison.OrdinalIgnoreCase)
        &&(names.Mononym?string.IsNullOrWhiteSpace(last):Part(last)is{}family&&string.Equals(names.LastName,family,StringComparison.OrdinalIgnoreCase));
}
