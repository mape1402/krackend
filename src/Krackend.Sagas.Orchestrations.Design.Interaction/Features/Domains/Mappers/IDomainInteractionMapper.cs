using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Storage;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Maps domain entities to interaction models.
/// </summary>
public interface IDomainInteractionMapper
{
    /// <summary>
    /// Maps one domain to interaction model.
    /// </summary>
    /// <param name="source">Source domain.</param>
    /// <returns>Mapped model.</returns>
    DomainModel ToModel(Domain source);

    /// <summary>
    /// Maps paged domain results to interaction paged result.
    /// </summary>
    /// <param name="source">Paged source.</param>
    /// <returns>Paged interaction model result.</returns>
    InteractionPagedResult<DomainModel> ToPagedModel(PagedResult<Domain> source);
}
