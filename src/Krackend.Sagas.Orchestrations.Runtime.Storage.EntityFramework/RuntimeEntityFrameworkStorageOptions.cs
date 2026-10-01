#nullable enable

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;

/// <summary>
/// Provides host-level customization options for the runtime Entity Framework storage adapter.
/// </summary>
public sealed class RuntimeEntityFrameworkStorageOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether Data Protection keys used for distribution secrets are stored in the runtime database.
    /// </summary>
    public bool PersistDataProtectionKeysToStorage { get; set; } = true;

    /// <summary>
    /// Gets or sets the stable Data Protection application name shared by all runtime replicas.
    /// </summary>
    public string DataProtectionApplicationName { get; set; } = "Krackend.Sagas.Orchestrations.Runtime";

    /// <summary>
    /// Gets or sets an optional callback that can replace or harden the Data Protection key storage configured by this adapter.
    /// </summary>
    public Action<IDataProtectionBuilder>? ConfigureDataProtection { get; set; }

    /// <summary>
    /// Gets or sets an optional callback that customizes the Entity Framework model after the portable default mappings are applied.
    /// </summary>
    public Action<ModelBuilder>? ConfigureModel { get; set; }
}
