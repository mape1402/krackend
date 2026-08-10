using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

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
    }
}


