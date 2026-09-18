#nullable enable

using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Security.Storage.EntityFramework;

/// <summary>
/// Provides host-level customization options for the Krackend security Entity Framework storage adapter.
/// </summary>
public sealed class KrackendSecurityEntityFrameworkStorageOptions
{
    /// <summary>
    /// Gets or sets an optional callback that customizes the Entity Framework model after the portable default mappings are applied.
    /// </summary>
    public Action<ModelBuilder>? ConfigureModel { get; set; }
}
