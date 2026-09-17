using FluentValidation;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates set stage execution condition command inputs.
/// </summary>
public sealed class SetStageExecutionConditionCommandValidator : AbstractValidator<SetStageExecutionConditionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetStageExecutionConditionCommandValidator"/> class.
    /// </summary>
    public SetStageExecutionConditionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.ExecutionCondition).Must(IsSupportedCondition);
    }

    private static bool IsSupportedCondition(ExecutionCondition condition)
        => condition is null ||
            condition.Engine == EngineType.DSL &&
            condition.Configuration is DslConditionConfiguration;
}
