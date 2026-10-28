namespace Swoovr.Client;

/// <summary>
/// Photo wording and links shared by the web and the app (D203). The API returns a signed path `api/media/{token}`:
/// the browser loads it through the web host's `/media/{token}`, the app straight from the API.
/// </summary>
public static class PhotoText
{
    public static string Web(string link) => "/media/" + link[(link.LastIndexOf('/') + 1)..];
    public static string App(Uri apiBase, string link) => new Uri(apiBase, link).ToString();

    /// <summary>What a member's own photo state means. Every refusal reads the same, whatever the reason.</summary>
    public static string State(string state) => state switch
    {
        "checking" => "Checking — not visible to anyone yet",
        "in-review" => "Being reviewed — not visible to anyone yet",
        "approved" => "Approved",
        "not-approved" => "Not approved — this photo can't be used on Swoovr",
        _ => "Removed",
    };

    /// <summary>#41: a chat photo's state as its sender sees it. The other person sees it only once it's approved.</summary>
    public static string ChatState(string? state) => state switch
    {
        "checking" => "Checking — they'll see it once it's approved",
        "in-review" => "Being reviewed — they'll see it once it's approved",
        "not-approved" => "Not approved — they won't see it",
        "removed" => "Removed",
        _ => "",
    };

    public const string ChatRules = "Photos are checked before the other person sees them. No nudity or sexual content.";

    /// <summary>#41, Q85: the fixed closing messages offered when ending a conversation.</summary>
    public static readonly IReadOnlyList<(string Id, string Label)> Closings =
    [
        ("thanks", "“Thanks for chatting. I don't think we're the right match, but I wish you all the best.”"),
        ("not-a-match", "“I've enjoyed our conversation, but I've decided not to continue. Take care.”"),
        ("take-care", "“I'm moving in a different direction, but thank you for your time. Wishing you well.”"),
    ];

    public const string ClosedTitle = "Conversation closed";
    public const string ClosedBody = "This conversation has ended. Your messages are still available to view, but you can no longer reply.";
    public const string ClosedSafety = "Report and block options remain available.";

    public const string EndHelp = "Ending disconnects you in every mode and closes this chat for both of you. You both keep the history. They can't ask to connect again for 30 days. It doesn't block or report them.";

    public const string Rules = "Photos are checked before anyone can see them. No nudity or sexual content, no one under 18, and only photos you have the right to share. Location details in photos are removed.";

    public static string Refusal(string? code) => code switch
    {
        "photo-too-big" => "Photos can be up to 10 MB.",
        "photo-unsupported" => "Use a JPEG, PNG or WebP photo (not animated).",
        "photo-invalid" => "That photo couldn't be used. Photos need to be at least 200 pixels on each side.",
        "photo-limit" => "You've reached the photo limit. Delete one first.",
        "upload-limit" => "You've uploaded a lot of photos in the last hour. Try again later.",
        "attachment-limit" => "You can send up to 20 photos a day.",
        "photos-off" => "This person isn't accepting photos in chat.",
        "photo-not-approved" => "Only approved photos can be shown.",
        "invalid-placement" => "Pick one main photo, or up to 6 photos for a mode.",
        "media-unavailable" => "Photos aren't available right now.",
        _ => "Something went wrong. Try again.",
    };
}
