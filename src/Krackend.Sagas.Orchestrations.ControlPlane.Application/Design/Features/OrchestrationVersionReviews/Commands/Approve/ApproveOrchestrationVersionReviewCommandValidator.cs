namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using FluentValidation;

/// <summary>Validates a lifecycle review approval.</summary>
public sealed class ApproveOrchestrationVersionReviewCommandValidator : AbstractValidator<ApproveOrchestrationVersionReviewCommand>
{
    public ApproveOrchestrationVersionReviewCommandValidator()
    {
        RuleFor(x => x.ReviewId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.ApprovedBy).NotEmpty().MaximumLength(128);
    }
}
