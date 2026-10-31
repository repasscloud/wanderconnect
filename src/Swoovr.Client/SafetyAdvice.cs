namespace Swoovr.Client;

/// <summary>#121: educational advice, never a legal-policy acceptance or proof of physical safety.</summary>
public static class SafetyAdvice
{
    public const string Version = "2026-10-30.1";
    public const string Title = "Before you connect or meet";
    public const string Link = "/safety/advice";
    public const string Introduction = "You choose who to connect with and when to meet. You can change your mind at any time.";
    public static IReadOnlyList<string> BeforeConnecting { get; } = Array.AsReadOnly(new[]
    {
        "Keep early conversations in Swoovr. Be cautious about pressure to move elsewhere, requests for money, or requests for private information.",
        "Share personal details at your own pace. A badge describes one check; it does not guarantee someone's identity, intentions or your safety.",
        "You can decline, disconnect, block or report. You do not owe anyone a meeting, an explanation or continued contact."
    });
    public static IReadOnlyList<string> BeforeMeeting { get; } = Array.AsReadOnly(new[]
    {
        "Choose a public place you are comfortable with and plan your own way there and home.",
        "Tell someone you trust about your plans and agree how you will check in. Swoovr meeting safety is optional, including on Free, and alerts are not guaranteed to arrive.",
        "Consent must be freely given and can change. Respect boundaries, stop if someone withdraws consent, and leave whenever you want to.",
        "If you feel unsafe, seek help that is appropriate for where you are. Safety & Support has country-specific resources. Swoovr does not monitor emergencies or dispatch responders."
    });
    public const string Responsibility = "Responsibility for harmful behaviour lies with the person who causes it. Following advice cannot guarantee your safety.";
    public const string ContinueLabel = "Continue";
    // Read it at first use and retain it in Profile and meeting planning. No local-storage acknowledgement, forced
    // checkbox or legal acceptance record: dismissal must never be represented as consent to terms or privacy policies.
    public static bool ShowDuringSetup(string? displayName) => string.IsNullOrWhiteSpace(displayName);
}
