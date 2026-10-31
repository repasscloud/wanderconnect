namespace Swoovr.Admin;

public static class RegistryText
{
    public static string Type(string value)=>value=="mobile"?"Mobile number":"Email";
    public static string Reason(string code)=>code=="permanent-account-ban"?"Permanent account ban":"Safety restriction";
    public static string Refusal(int status,string? code)=>status switch
    {
        400=>"Check the selection, required confirmations and explanation (up to 1,000 characters).",
        409=>"The record or restriction set changed, or this action is no longer eligible. Review the current records before trying again.",
        503=>"Restricted records cannot be checked right now. Refresh the record before trying again.",
        _=>AdminText.Refusal(status,code),
    };
    public static string Blocked(string? code)=>code switch
    {
        "account-not-restorable"=>"Deleted, deleting, locked or incomplete accounts cannot be reinstated here.",
        "contact-recovery-required"=>"A retired login requires independently approved original-account recovery before reinstatement.",
        "no-termination"=>"There is no account termination to reinstate.",
        "missing-enforcement-provenance"=>"Source enforcement records need restricted review and reconciliation before reinstatement.",
        "too-many-restrictions"=>"This restriction set needs a separate restricted review before reinstatement.",
        "identifier-review-needed"=>"Another identifier restriction needs separate review. Reinstating this account cannot release another case's restriction.",
        _=>"Reinstatement is not available. Review the current source records.",
    };
}
