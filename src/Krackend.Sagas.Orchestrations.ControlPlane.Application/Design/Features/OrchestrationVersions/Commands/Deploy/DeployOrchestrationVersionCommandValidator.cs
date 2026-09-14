using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates deploy orchestration version command inputs.
/// </summary>
public sealed class DeployOrchestrationVersionCommandValidator : AbstractValidator<DeployOrchestrationVersionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeployOrchestrationVersionCommandValidator"/> class.
    /// </summary>
    public DeployOrchestrationVersionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.UpdatedBy).NotEmpty().MaximumLength(128);
    }
}


