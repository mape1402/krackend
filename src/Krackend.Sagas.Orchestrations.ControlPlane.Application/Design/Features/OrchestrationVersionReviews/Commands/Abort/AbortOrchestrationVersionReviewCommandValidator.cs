namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using FluentValidation;

/// <summary>Validates a request to abort an unapplied lifecycle review.</summary>
public sealed class AbortOrchestrationVersionReviewCommandValidator : AbstractValidator<AbortOrchestrationVersionReviewCommand>
{
    public AbortOrchestrationVersionReviewCommandValidator()
    {
        RuleFor(x => x.ReviewId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.AbortedBy).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Reason).MaximumLength(2000);
    }
}
