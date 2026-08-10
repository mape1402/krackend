using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents update variable definition command.
/// </summary>
public sealed record UpdateVariableDefinitionCommand(
    string Id,
    string Key,
    string DisplayName,
    string Description,
    VariableScope Scope,
    VariableValueType ValueType,
    string DefaultValue,
    bool IsRequired,
    bool IsSensitive) : IRequest<bool>;

