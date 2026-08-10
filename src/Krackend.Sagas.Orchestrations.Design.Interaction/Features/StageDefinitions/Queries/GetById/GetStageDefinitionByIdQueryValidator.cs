using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates get stage definition by id query inputs.
/// </summary>
public sealed class GetStageDefinitionByIdQueryValidator : AbstractValidator<GetStageDefinitionByIdQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetStageDefinitionByIdQueryValidator"/> class.
    /// </summary>
    public GetStageDefinitionByIdQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

