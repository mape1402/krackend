using FluentValidation;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates update stage definition command inputs.
/// </summary>
public sealed class UpdateStageDefinitionCommandValidator : AbstractValidator<UpdateStageDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateStageDefinitionCommandValidator"/> class.
    /// </summary>
    public UpdateStageDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Order).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExecutionCondition).Must(IsSupportedCondition);
    }

    private static bool IsSupportedCondition(ExecutionCondition condition)
        => condition is null ||
            condition.Engine == EngineType.DSL &&
            condition.Configuration is DslConditionConfiguration;
}

