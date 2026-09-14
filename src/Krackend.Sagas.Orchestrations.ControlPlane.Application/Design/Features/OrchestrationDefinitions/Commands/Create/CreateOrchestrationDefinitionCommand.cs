using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents create orchestration definition command.
/// </summary>
public sealed record CreateOrchestrationDefinitionCommand(
    string Key,
    string Name,
    string DomainId,
    string Description,
    string OwnerTeamId,
    IEnumerable<string> Tags,
    string CreatedBy) : IRequest<string>;


