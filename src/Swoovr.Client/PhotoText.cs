namespace Swoovr.Client;

/// <summary>
/// Photo wording and links shared by the web and the app (D203). The API returns a signed path `api/v1/media/{token}`:
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

    public const string Rules = "Photos are checked before anyone can see them. No nudity or sexual content, no one under 18, and only photos you have the right to share. Location details in photos are removed.";

    public static string Refusal(string? code) => code switch
    {
        "photo-too-big" => "Photos can be up to 10 MB.",
        "photo-unsupported" => "Use a JPEG, PNG or WebP photo (not animated).",
        "photo-invalid" => "That photo couldn't be used. Photos need to be at least 200 pixels on each side.",
        "photo-limit" => "You've reached the photo limit. Delete one first.",
        "upload-limit" => "You've uploaded a lot of photos in the last hour. Try again later.",
        "photo-not-approved" => "Only approved photos can be shown.",
        "invalid-placement" => "Pick one main photo, or up to 6 photos for a mode.",
        "media-unavailable" => "Photos aren't available right now.",
        _ => "Something went wrong. Try again.",
    };
}
