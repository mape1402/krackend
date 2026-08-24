using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents delete branch rule definition command.
/// </summary>
public sealed record DeleteBranchRuleDefinitionCommand(string Id) : IRequest<bool>;

