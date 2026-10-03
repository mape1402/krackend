using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents a query for task compensation schema context.
/// </summary>
public sealed record GetTaskCompensationSchemaContextQuery(
    string OrchestrationVersionId,
    string TaskDefinitionId) : IRequest<OrchestrationSchemaContext>;

