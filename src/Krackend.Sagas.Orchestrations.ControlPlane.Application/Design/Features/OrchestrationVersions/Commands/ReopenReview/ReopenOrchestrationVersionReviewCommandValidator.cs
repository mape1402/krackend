using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates reopen orchestration version review command inputs.
/// </summary>
public sealed class ReopenOrchestrationVersionReviewCommandValidator : AbstractValidator<ReopenOrchestrationVersionReviewCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReopenOrchestrationVersionReviewCommandValidator"/> class.
    /// </summary>
    public ReopenOrchestrationVersionReviewCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.UpdatedBy).NotEmpty().MaximumLength(128);
    }
}


