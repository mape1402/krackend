using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates get stage definitions query inputs.
/// </summary>
public sealed class GetStageDefinitionsQueryValidator : AbstractValidator<GetStageDefinitionsQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetStageDefinitionsQueryValidator"/> class.
    /// </summary>
    public GetStageDefinitionsQueryValidator()
    {
        RuleFor(x => x.OrchestrationVersionId).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

