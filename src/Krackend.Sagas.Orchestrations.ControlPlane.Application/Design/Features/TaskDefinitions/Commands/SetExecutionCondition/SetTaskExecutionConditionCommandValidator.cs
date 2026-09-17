using FluentValidation;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates set task execution condition command inputs.
/// </summary>
public sealed class SetTaskExecutionConditionCommandValidator : AbstractValidator<SetTaskExecutionConditionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetTaskExecutionConditionCommandValidator"/> class.
    /// </summary>
    public SetTaskExecutionConditionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.ExecutionCondition).Must(IsSupportedCondition);
    }

    private static bool IsSupportedCondition(ExecutionCondition condition)
        => condition is null ||
            condition.Engine == EngineType.DSL &&
            condition.Configuration is DslConditionConfiguration;
}
