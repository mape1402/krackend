namespace Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;

using System.Text.Json.Nodes;

internal sealed record RealMessagingServiceOutcome(
    RealMessagingServiceOutcomeKind Kind,
    JsonNode? Payload,
    string? ErrorCode,
    string? ErrorMessage,
    bool? IsRetryableCandidate,
    TimeSpan Delay)
{
    public static RealMessagingServiceOutcome Success(JsonNode payload)
        => new(RealMessagingServiceOutcomeKind.Success, payload, null, null, null, TimeSpan.Zero);

    public static RealMessagingServiceOutcome DelayedSuccess(JsonNode payload, TimeSpan delay)
        => new(RealMessagingServiceOutcomeKind.Success, payload, null, null, null, delay);

    public static RealMessagingServiceOutcome Failure(
        string errorCode,
        string errorMessage,
        bool? isRetryableCandidate = null)
        => new(RealMessagingServiceOutcomeKind.Failure, null, errorCode, errorMessage, isRetryableCandidate, TimeSpan.Zero);

    public static RealMessagingServiceOutcome NoReply()
        => new(RealMessagingServiceOutcomeKind.NoReply, null, null, null, null, TimeSpan.Zero);
}
