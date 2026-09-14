using FluentValidation;

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
        RuleFor(x => x.FromId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.NavigateToId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Condition).NotNull();
    }
}

