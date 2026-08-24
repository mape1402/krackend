using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates update trigger binding command inputs.
/// </summary>
public sealed class UpdateTriggerBindingCommandValidator : AbstractValidator<UpdateTriggerBindingCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTriggerBindingCommandValidator"/> class.
    /// </summary>
    public UpdateTriggerBindingCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.TriggerChannel).NotNull();
    }
}

