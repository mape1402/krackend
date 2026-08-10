using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents create variable definition command.
/// </summary>
public sealed record CreateVariableDefinitionCommand(
    string OrchestrationVersionId,
    string Key,
    string DisplayName,
    string Description,
    VariableScope Scope,
    VariableValueType ValueType,
    string DefaultValue,
    bool IsRequired,
    bool IsSensitive) : IRequest<string>;

