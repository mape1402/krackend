namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Manages Runtime-side Design node connection credentials and validation.
/// </summary>
public interface IRuntimeDesignNodeConnectionService
{
    /// <summary>
    /// Generates an inbound credential package that Design can import.
    /// </summary>
    /// <param name="designNodeId">Design node id.</param>
    /// <param name="issuerBaseUrl">Runtime base URL to include in the package.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated package.</returns>
    Task<RuntimeDesignNodeCredentialPackageModel> GenerateCredentialPackageAsync(
        string designNodeId,
        string issuerBaseUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a Design-generated credential package for outbound Runtime calls.
    /// </summary>
    /// <param name="input">Import input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ImportCredentialPackageAsync(
        ImportRuntimeDesignNodeCredentialPackageInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates outbound connectivity from Runtime to Design.
    /// </summary>
    /// <param name="designNodeId">Design node id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result.</returns>
    Task<RuntimeDesignNodeConnectionValidationModel> ValidateConnectionAsync(
        string designNodeId,
        CancellationToken cancellationToken = default);
}
