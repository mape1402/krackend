namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using FluentValidation;

/// <summary>
/// Validates review requests.
/// </summary>
public sealed class RequestOrchestrationVersionReviewCommandValidator : AbstractValidator<RequestOrchestrationVersionReviewCommand>
{
    /// <summary>Initializes a new instance.</summary>
    public RequestOrchestrationVersionReviewCommandValidator()
    {
        RuleFor(x => x.OrchestrationVersionId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Operation).IsInEnum();
        RuleFor(x => x.RequestedBy).NotEmpty().MaximumLength(128);
    }
}
