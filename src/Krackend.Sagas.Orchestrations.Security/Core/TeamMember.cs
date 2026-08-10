namespace Krackend.Sagas.Orchestrations.Security.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents membership of an external identity in a team.
/// </summary>
public sealed class TeamMember
{
    /// <summary>
    /// Gets or sets membership identifier.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets team identifier.
    /// </summary>
    public Id TeamId { get; set; }

    /// <summary>
    /// Gets or sets external identity identifier.
    /// </summary>
    public required string ExternalUserId { get; set; }

    /// <summary>
    /// Gets or sets optional display name snapshot.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets created timestamp in UTC.
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }
}
