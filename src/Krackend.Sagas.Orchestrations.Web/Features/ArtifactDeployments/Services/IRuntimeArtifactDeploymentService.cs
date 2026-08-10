namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Handles runtime artifact deployment use cases.
/// </summary>
public interface IRuntimeArtifactDeploymentService
{
    Task<RuntimeArtifactDeploymentResult> Deploy(
        RuntimeArtifactDeploymentRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RuntimeArtifactModel>> GetAll(CancellationToken cancellationToken = default);

    Task<RuntimeArtifactModel> GetActive(
        string orchestrationDefinitionKey,
        CancellationToken cancellationToken = default);
}
