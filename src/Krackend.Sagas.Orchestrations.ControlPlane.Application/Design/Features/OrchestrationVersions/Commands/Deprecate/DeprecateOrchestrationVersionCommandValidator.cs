using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates deprecate orchestration version command inputs.
/// </summary>
public sealed class DeprecateOrchestrationVersionCommandValidator : AbstractValidator<DeprecateOrchestrationVersionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeprecateOrchestrationVersionCommandValidator"/> class.
    /// </summary>
    public DeprecateOrchestrationVersionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.UpdatedBy).NotEmpty().MaximumLength(128);
    }
}


