using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents update orchestration definition command.
/// </summary>
public sealed record UpdateOrchestrationDefinitionCommand(
    string Id,
    string Name,
    string DomainId,
    string Description,
    string OwnerTeamId,
    IEnumerable<string> Tags,
    string UpdatedBy) : IRequest<bool>;


