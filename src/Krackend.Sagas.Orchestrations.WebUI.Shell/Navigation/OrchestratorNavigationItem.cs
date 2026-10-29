namespace Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

public sealed class OrchestratorNavigationItem
{
    public required string Label { get; init; }
    public required string Area { get; init; }
    public required string Page { get; init; }
    /// <summary>
    /// Gets an optional secondary section used by a module to organize its own navigation.
    /// </summary>
    public string Section { get; init; } = string.Empty;
    public int Order { get; init; }
}
