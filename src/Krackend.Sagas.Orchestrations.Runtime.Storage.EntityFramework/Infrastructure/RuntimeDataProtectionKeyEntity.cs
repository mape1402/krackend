namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

/// <summary>
/// Stores ASP.NET Data Protection key material for durable distribution secrets.
/// </summary>
public sealed class RuntimeDataProtectionKeyEntity
{
    /// <summary>
    /// Gets or sets the surrogate key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the friendly key name assigned by Data Protection.
    /// </summary>
    public string FriendlyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized Data Protection key XML.
    /// </summary>
    public string Xml { get; set; } = string.Empty;
}
