using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates update orchestration version command inputs.
/// </summary>
public sealed class UpdateOrchestrationVersionCommandValidator : AbstractValidator<UpdateOrchestrationVersionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOrchestrationVersionCommandValidator"/> class.
    /// </summary>
    public UpdateOrchestrationVersionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Checksum).NotEmpty();
        RuleFor(x => x.UpdatedBy).NotEmpty().MaximumLength(128);
        RuleFor(x => x.MaxInstanceDurationSeconds).GreaterThanOrEqualTo(0)
            .When(x => x.UpdateInstanceDurations && x.MaxInstanceDurationSeconds.HasValue);
        RuleFor(x => x.IdleDurationSeconds).GreaterThan(0)
            .When(x => x.UpdateInstanceDurations && x.IdleDurationSeconds.HasValue);
    }
}


