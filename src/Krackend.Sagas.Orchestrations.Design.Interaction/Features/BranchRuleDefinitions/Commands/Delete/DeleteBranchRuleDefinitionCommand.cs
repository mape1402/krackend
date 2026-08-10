using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents delete branch rule definition command.
/// </summary>
public sealed record DeleteBranchRuleDefinitionCommand(string Id) : IRequest<bool>;

