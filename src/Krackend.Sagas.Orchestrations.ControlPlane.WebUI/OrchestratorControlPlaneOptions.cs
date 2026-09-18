using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI;

/// <summary>
/// Configures the full orchestration control-plane composition.
/// </summary>
public sealed class OrchestratorControlPlaneOptions
{
    /// <summary>
    /// Gets or sets the route prefix used by the control-plane administration UI.
    /// </summary>
    public string AdminRootPath { get; set; } = "admin";

    /// <summary>
    /// Gets or sets the default schema registry provider key used when creating schema bindings from the designer UI.
    /// </summary>
    public string DefaultSchemaRegistryProviderKey { get; set; } = "knowl";

    /// <summary>
    /// Gets or sets the Entity Framework storage configuration used by the control plane.
    /// </summary>
    public Action<DbContextOptionsBuilder> ConfigureStorage { get; set; }

    /// <summary>
    /// Gets or sets the optional Entity Framework model customization applied by the host after the portable storage model is configured.
    /// </summary>
    public Action<ControlPlaneEntityFrameworkStorageOptions> ConfigureStorageModel { get; set; }
}
