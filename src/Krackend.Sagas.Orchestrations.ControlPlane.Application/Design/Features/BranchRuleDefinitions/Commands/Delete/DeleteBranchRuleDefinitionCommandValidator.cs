using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates delete branch rule definition command inputs.
/// </summary>
public sealed class DeleteBranchRuleDefinitionCommandValidator : AbstractValidator<DeleteBranchRuleDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteBranchRuleDefinitionCommandValidator"/> class.
    /// </summary>
    public DeleteBranchRuleDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

