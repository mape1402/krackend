using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates approve orchestration version command inputs.
/// </summary>
public sealed class ApproveOrchestrationVersionCommandValidator : AbstractValidator<ApproveOrchestrationVersionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApproveOrchestrationVersionCommandValidator"/> class.
    /// </summary>
    public ApproveOrchestrationVersionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.ApprovedBy).NotEmpty().MaximumLength(128);
    }
}


