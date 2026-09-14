namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Represents the result of authenticating a control-plane request against the runtime.
/// </summary>
public sealed class RuntimeArtifactDeliveryEndpointAuthenticationResult
{
    private RuntimeArtifactDeliveryEndpointAuthenticationResult(bool succeeded, string message)
    {
        Succeeded = succeeded;
        Message = message;
    }

    /// <summary>
    /// Gets a value indicating whether authentication succeeded.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets the authentication message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets or sets the authenticated source key.
    /// </summary>
    public string SourceKey { get; init; }

    /// <summary>
    /// Creates a successful authentication result.
    /// </summary>
    public static RuntimeArtifactDeliveryEndpointAuthenticationResult Success(string sourceKey)
        => new(true, string.Empty) { SourceKey = sourceKey };

    /// <summary>
    /// Creates a failed authentication result.
    /// </summary>
    public static RuntimeArtifactDeliveryEndpointAuthenticationResult Failure(string message)
        => new(false, message);
}
