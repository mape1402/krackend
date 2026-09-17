using FluentValidation;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates update branch rule definition command inputs.
/// </summary>
public sealed class UpdateBranchRuleDefinitionCommandValidator : AbstractValidator<UpdateBranchRuleDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateBranchRuleDefinitionCommandValidator"/> class.
    /// </summary>
    public UpdateBranchRuleDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.FromType).Equal(ElementType.Stage);
        RuleFor(x => x.FromId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.NavigateToType).Equal(ElementType.Stage);
        RuleFor(x => x.NavigateToId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Condition).NotNull();
        RuleFor(x => x.Condition).Must(IsSupportedCondition);
    }

    private static bool IsSupportedCondition(ExecutionCondition condition)
        => condition is not null &&
            condition.Engine == EngineType.DSL &&
            condition.Configuration is DslConditionConfiguration;
}

