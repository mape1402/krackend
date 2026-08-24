namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Represents the outcome of validating an artifact delivery signature.
/// </summary>
public sealed class ArtifactDeliverySignatureValidationResult
{
    private ArtifactDeliverySignatureValidationResult(bool succeeded, string message)
    {
        Succeeded = succeeded;
        Message = message;
    }

    /// <summary>
    /// Gets a value indicating whether validation succeeded.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets the validation message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ArtifactDeliverySignatureValidationResult Success()
        => new(true, string.Empty);

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static ArtifactDeliverySignatureValidationResult Failure(string message)
        => new(false, message);
}
