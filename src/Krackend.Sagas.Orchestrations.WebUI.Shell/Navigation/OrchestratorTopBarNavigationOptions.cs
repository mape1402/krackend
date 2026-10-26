namespace Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

/// <summary>Host-configurable behavior for contextual top-bar navigation.</summary>
public sealed class OrchestratorTopBarNavigationOptions
{
    /// <summary>Gets or sets whether Krackend resolves its default contextual actions.</summary>
    public bool EnableContextualBackNavigation { get; set; } = true;

    /// <summary>Gets host overrides keyed as <c>area:page</c>.</summary>
    public IDictionary<string, OrchestratorTopBarBackOverrideOptions> Overrides { get; } =
        new Dictionary<string, OrchestratorTopBarBackOverrideOptions>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Defines a host-owned override for one top-bar back action.</summary>
public sealed class OrchestratorTopBarBackOverrideOptions
{
    /// <summary>Gets or sets whether the action is visible.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Gets or sets the accessible label and tooltip.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the relative or HTTP(S) navigation target.</summary>
    public string Url { get; set; } = string.Empty;
}
