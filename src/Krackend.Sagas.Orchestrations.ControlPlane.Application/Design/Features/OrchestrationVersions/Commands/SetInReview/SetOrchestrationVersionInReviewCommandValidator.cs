using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates set orchestration version in review command inputs.
/// </summary>
public sealed class SetOrchestrationVersionInReviewCommandValidator : AbstractValidator<SetOrchestrationVersionInReviewCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetOrchestrationVersionInReviewCommandValidator"/> class.
    /// </summary>
    public SetOrchestrationVersionInReviewCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}


