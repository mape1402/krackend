#nullable enable

using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework;

/// <summary>
/// Provides host-level customization options for the control-plane Entity Framework storage adapter.
/// </summary>
public sealed class ControlPlaneEntityFrameworkStorageOptions
{
    /// <summary>
    /// Gets or sets an optional callback that customizes the Entity Framework model after the portable default mappings are applied.
    /// </summary>
    public Action<ModelBuilder>? ConfigureModel { get; set; }
}
