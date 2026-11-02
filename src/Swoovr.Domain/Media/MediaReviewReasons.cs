namespace Swoovr.Domain;

public sealed record MediaReviewMetadata(string Code,string? Notes);
public sealed record MediaReviewReasonDefinition(string Code,string Decision,bool RequiresNotes=false,bool Selectable=true);

/// <summary>DEFAULT review taxonomy; never changes eligibility or the existing escalation path.</summary>
public static class MediaReviewReasons
{
    public static IReadOnlyList<MediaReviewReasonDefinition> All { get; }=Array.AsReadOnly<MediaReviewReasonDefinition>([
        new("APPROVED_STANDARD","approve"),new("APPROVED_NON_EXPLICIT","approve"),new("APPROVED_AFTER_REVIEW","approve"),new("APPROVED_FALSE_POSITIVE","approve"),
        new("REJECT_EXPLICIT_BETA","reject"),new("REJECT_IMPERSONATION","reject"),new("REJECT_PRIVACY","reject"),new("REJECT_VIOLENCE","reject"),
        new("REJECT_HATE","reject"),new("REJECT_SPAM","reject"),new("REJECT_IMAGE_QUALITY","reject"),new("REJECT_OTHER","reject",true),
        new("ESCALATE_CHILD_SAFETY","escalate"),new("ESCALATE_NON_CONSENSUAL","escalate"),new("ESCALATE_OTHER","escalate"),
        new("LEGACY_APPROVE","approve",Selectable:false),new("LEGACY_REJECT","reject",Selectable:false),new("LEGACY_ESCALATE","escalate",Selectable:false),
    ]);

    public static MediaReviewMetadata? Create(string? decision,string? code,string? notes)
    {
        if(decision is not("approve" or "reject" or "escalate")||notes is{Length:>MediaPolicy.ReasonMax})return null;
        var legacy=code is null;
        code??=decision switch{"approve"=>"LEGACY_APPROVE","reject"=>"LEGACY_REJECT",_=>"LEGACY_ESCALATE"};
        var reason=All.SingleOrDefault(r=>r.Code==code&&r.Decision==decision&&(legacy||r.Selectable));
        notes=string.IsNullOrWhiteSpace(notes)?null:notes.Trim();
        if(reason is null||reason.RequiresNotes&&notes is null)return null;
        // Approval can later be superseded. Do not silently create/discard private approval-note history.
        // Legacy approval notes were ignored; coded clients must omit them until retained history exists.
        if(decision=="approve"){if(!legacy&&notes is not null)return null;notes=null;}
        return new(code,notes);
    }
}
