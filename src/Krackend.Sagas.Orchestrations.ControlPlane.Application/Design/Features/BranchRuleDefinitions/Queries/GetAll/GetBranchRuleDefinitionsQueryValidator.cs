using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates get branch rule definitions query inputs.
/// </summary>
public sealed class GetBranchRuleDefinitionsQueryValidator : AbstractValidator<GetBranchRuleDefinitionsQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetBranchRuleDefinitionsQueryValidator"/> class.
    /// </summary>
    public GetBranchRuleDefinitionsQueryValidator()
    {
        RuleFor(x => x.StageDefinitionId).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

