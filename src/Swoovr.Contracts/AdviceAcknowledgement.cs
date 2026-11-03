namespace Swoovr.Contracts;

public sealed record AdviceAcknowledgementView(string Revision,bool Acknowledged,DateTimeOffset? AcknowledgedAt)
{
    public const string CurrentRevision="2026-10-30.1";
}
public sealed record AdviceAcknowledgementRequest(string Revision,bool Confirmed);
