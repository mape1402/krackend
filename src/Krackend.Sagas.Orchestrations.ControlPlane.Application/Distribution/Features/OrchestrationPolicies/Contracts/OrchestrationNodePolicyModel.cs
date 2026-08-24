namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public sealed class OrchestrationNodePolicyModel
{
    public string OrchestrationDefinitionId { get; set; }
    public IReadOnlyCollection<string> RuntimeNodeIds { get; set; } = Array.Empty<string>();
}

public sealed record ReplaceOrchestrationNodePolicyInput(
    string OrchestrationDefinitionId,
    IReadOnlyCollection<string> RuntimeNodeIds,
    string UpdatedBy);

