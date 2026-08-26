namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Manages Design-side Runtime node connection credentials and validation.
/// </summary>
public interface IRuntimeNodeConnectionApplicationService
{
    /// <summary>
    /// Generates an inbound credential package that Runtime can import.
    /// </summary>
    /// <param name="runtimeNodeId">Runtime node id.</param>
    /// <param name="issuerBaseUrl">Design base URL to include in the package.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated package.</returns>
    Task<RuntimeNodeCredentialPackageModel> GenerateCredentialPackage(
        string runtimeNodeId,
        string issuerBaseUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a Runtime-generated credential package for outbound Design calls.
    /// </summary>
    /// <param name="input">Import input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ImportCredentialPackage(
        ImportRuntimeNodeCredentialPackageInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates outbound connectivity from Design to Runtime.
    /// </summary>
    /// <param name="runtimeNodeId">Runtime node id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result.</returns>
    Task<RuntimeNodeConnectionValidationModel> ValidateConnection(
        string runtimeNodeId,
        CancellationToken cancellationToken = default);
}
