using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates create branch rule definition command inputs.
/// </summary>
public sealed class CreateBranchRuleDefinitionCommandValidator : AbstractValidator<CreateBranchRuleDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateBranchRuleDefinitionCommandValidator"/> class.
    /// </summary>
    public CreateBranchRuleDefinitionCommandValidator()
    {
        RuleFor(x => x.FromId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.NavigateToId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Condition).NotNull();
    }
}

