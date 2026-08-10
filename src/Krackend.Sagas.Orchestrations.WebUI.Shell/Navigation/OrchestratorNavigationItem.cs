namespace Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

public sealed class OrchestratorNavigationItem
{
    public required string Label { get; init; }
    public required string Area { get; init; }
    public required string Page { get; init; }
    public int Order { get; init; }
}
