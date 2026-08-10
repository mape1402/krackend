using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates get branch rule definition by id query inputs.
/// </summary>
public sealed class GetBranchRuleDefinitionByIdQueryValidator : AbstractValidator<GetBranchRuleDefinitionByIdQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetBranchRuleDefinitionByIdQueryValidator"/> class.
    /// </summary>
    public GetBranchRuleDefinitionByIdQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

