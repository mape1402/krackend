namespace Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;

internal sealed class RealMessagingServiceException : Exception
{
    public RealMessagingServiceException(
        string errorCode,
        string message,
        bool? isRetryableCandidate = null)
        : base(message)
    {
        ErrorCode = errorCode;
        IsRetryableCandidate = isRetryableCandidate;
    }

    public string ErrorCode { get; }

    public bool? IsRetryableCandidate { get; }
}
