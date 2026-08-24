using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Maps domain entities to interaction models.
/// </summary>
public interface IDomainApplicationMapper
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
    ApplicationPagedResult<DomainModel> ToPagedModel(PagedResult<Domain> source);
}
