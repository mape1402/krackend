namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Represents a logical deployment environment exposed by the distribution application layer.
/// </summary>
public sealed class DistributionEnvironmentModel
{
    /// <summary>
    /// Gets or sets the environment id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the stable environment code.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the optional description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the environment is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets when the environment was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the environment was last updated.
    /// </summary>
    public DateTime? UpdatedAtUtc { get; set; }
}

/// <summary>
/// Captures values required to create or update a distribution environment.
/// </summary>
public sealed record UpsertDistributionEnvironmentInput(
    /// <summary>
    /// Environment id when updating an existing environment.
    /// </summary>
    string EnvironmentId,
    /// <summary>
    /// Environment display name.
    /// </summary>
    string Name,
    /// <summary>
    /// Environment code.
    /// </summary>
    string Code,
    /// <summary>
    /// Environment description.
    /// </summary>
    string Description,
    /// <summary>
    /// Whether the environment is enabled.
    /// </summary>
    bool IsEnabled);
