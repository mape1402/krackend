namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using FluentValidation;

/// <summary>Validates a lifecycle review rejection.</summary>
public sealed class RejectOrchestrationVersionReviewCommandValidator : AbstractValidator<RejectOrchestrationVersionReviewCommand>
{
    public RejectOrchestrationVersionReviewCommandValidator()
    {
        RuleFor(x => x.ReviewId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.RejectedBy).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Comment).MaximumLength(2000);
    }
}
