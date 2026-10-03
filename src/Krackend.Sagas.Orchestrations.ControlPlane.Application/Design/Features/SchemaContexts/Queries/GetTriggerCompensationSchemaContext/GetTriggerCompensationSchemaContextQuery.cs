using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents a query for trigger compensation schema context.
/// </summary>
public sealed record GetTriggerCompensationSchemaContextQuery(
    string OrchestrationVersionId,
    string TriggerBindingId) : IRequest<OrchestrationSchemaContext>;

