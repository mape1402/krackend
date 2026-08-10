using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates get orchestration version by id query inputs.
/// </summary>
public sealed class GetOrchestrationVersionByIdQueryValidator : AbstractValidator<GetOrchestrationVersionByIdQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetOrchestrationVersionByIdQueryValidator"/> class.
    /// </summary>
    public GetOrchestrationVersionByIdQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}


