namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using FluentValidation;

/// <summary>Validates review application commands.</summary>
public sealed class ApplyOrchestrationVersionReviewCommandValidator : AbstractValidator<ApplyOrchestrationVersionReviewCommand>
{
    /// <summary>Initializes a new instance.</summary>
    public ApplyOrchestrationVersionReviewCommandValidator()
    {
        RuleFor(x => x.ReviewId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.AppliedBy).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Version).MaximumLength(64);
        RuleFor(x => x.VersionLabel).MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
