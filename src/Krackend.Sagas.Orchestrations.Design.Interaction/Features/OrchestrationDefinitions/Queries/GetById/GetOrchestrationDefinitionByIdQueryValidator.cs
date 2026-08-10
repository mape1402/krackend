using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates get orchestration definition by id query inputs.
/// </summary>
public sealed class GetOrchestrationDefinitionByIdQueryValidator : AbstractValidator<GetOrchestrationDefinitionByIdQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetOrchestrationDefinitionByIdQueryValidator"/> class.
    /// </summary>
    public GetOrchestrationDefinitionByIdQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}


