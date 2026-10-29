namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>Exports and imports isolated orchestration drafts.</summary>
public interface IOrchestrationVersionPackageService
{
    Task<OrchestrationVersionPackage> ExportAsync(string orchestrationVersionId, CancellationToken cancellationToken = default);

    OrchestrationVersionPackageInspection Inspect(string packageJson);

    string Serialize(OrchestrationVersionPackage package);

    Task<string> ImportAsync(
        string orchestrationDefinitionId,
        string packageJson,
        string importedBy,
        CancellationToken cancellationToken = default);
}
