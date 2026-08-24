using Krackend.Sagas.Orchestrations.ControlPlane.Security.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Security.Entities;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Security.Mappings;

/// <summary>
/// Maps security domain models and persistence entities.
/// </summary>
internal static class SecurityEntityMapper
{
    /// <summary>
    /// Maps team model to entity.
    /// </summary>
    /// <param name="source">Source model.</param>
    /// <returns>Entity instance.</returns>
    public static TeamEntity ToEntity(this Team source)
    {
        return new TeamEntity
        {
            Id = source.Id,
            Key = source.Key,
            DisplayName = source.DisplayName,
            Description = source.Description,
            IsActive = source.IsActive,
            CreatedOnUtc = source.CreatedOnUtc,
            UpdatedOnUtc = source.UpdatedOnUtc,
        };
    }

    /// <summary>
    /// Maps team entity to model.
    /// </summary>
    /// <param name="source">Source entity.</param>
    /// <returns>Domain model.</returns>
    public static Team ToDefinition(this TeamEntity source)
    {
        return new Team
        {
            Id = source.Id,
            Key = source.Key,
            DisplayName = source.DisplayName,
            Description = source.Description,
            IsActive = source.IsActive,
            CreatedOnUtc = source.CreatedOnUtc,
            UpdatedOnUtc = source.UpdatedOnUtc,
        };
    }

    /// <summary>
    /// Maps team member model to entity.
    /// </summary>
    /// <param name="source">Source model.</param>
    /// <returns>Entity instance.</returns>
    public static TeamMemberEntity ToEntity(this TeamMember source)
    {
        return new TeamMemberEntity
        {
            Id = source.Id,
            TeamId = source.TeamId,
            ExternalUserId = source.ExternalUserId,
            DisplayName = source.DisplayName,
            CreatedOnUtc = source.CreatedOnUtc,
        };
    }

    /// <summary>
    /// Maps team member entity to model.
    /// </summary>
    /// <param name="source">Source entity.</param>
    /// <returns>Domain model.</returns>
    public static TeamMember ToDefinition(this TeamMemberEntity source)
    {
        return new TeamMember
        {
            Id = source.Id,
            TeamId = source.TeamId,
            ExternalUserId = source.ExternalUserId,
            DisplayName = source.DisplayName,
            CreatedOnUtc = source.CreatedOnUtc,
        };
    }
}
