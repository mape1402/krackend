using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Sieve.Attributes;

namespace Krackend.Sagas.Orchestrations.Security.Storage.SqlServer.Entities;

/// <summary>
/// Represents persisted team member entity.
/// </summary>
public sealed class TeamMemberEntity
{
    /// <summary>
    /// Gets or sets identifier.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets team identifier.
    /// </summary>
    public Id TeamId { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "externalUserId")]
    /// <summary>
    /// Gets or sets external user identifier.
    /// </summary>
    public string ExternalUserId { get; set; }

    /// <summary>
    /// Gets or sets display name snapshot.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets created timestamp.
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets parent team.
    /// </summary>
    public TeamEntity Team { get; set; }
}
