namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Represents the result of runtime artifact compatibility validation.
/// </summary>
public sealed class RuntimeArtifactCompatibilityValidationResult
{
    /// <summary>
    /// Gets whether the artifact is compatible with the runtime.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the compatibility error code when validation fails.
    /// </summary>
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>
    /// Gets the compatibility error message when validation fails.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Creates a successful compatibility result.
    /// </summary>
    /// <returns>A successful compatibility result.</returns>
    public static RuntimeArtifactCompatibilityValidationResult Success()
        => new()
        {
            Succeeded = true
        };

    /// <summary>
    /// Creates a failed compatibility result.
    /// </summary>
    /// <param name="errorCode">Compatibility error code.</param>
    /// <param name="errorMessage">Compatibility error message.</param>
    /// <returns>A failed compatibility result.</returns>
    public static RuntimeArtifactCompatibilityValidationResult Failure(string errorCode, string errorMessage)
        => new()
        {
            Succeeded = false,
            ErrorCode = errorCode ?? string.Empty,
            ErrorMessage = errorMessage ?? string.Empty
        };
}
