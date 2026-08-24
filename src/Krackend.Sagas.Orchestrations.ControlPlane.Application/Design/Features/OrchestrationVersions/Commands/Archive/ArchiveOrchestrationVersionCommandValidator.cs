using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates archive orchestration version command inputs.
/// </summary>
public sealed class ArchiveOrchestrationVersionCommandValidator : AbstractValidator<ArchiveOrchestrationVersionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArchiveOrchestrationVersionCommandValidator"/> class.
    /// </summary>
    public ArchiveOrchestrationVersionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.UpdatedBy).NotEmpty().MaximumLength(128);
    }
}


