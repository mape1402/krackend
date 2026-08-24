using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates disable trigger binding command inputs.
/// </summary>
public sealed class DisableTriggerBindingCommandValidator : AbstractValidator<DisableTriggerBindingCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisableTriggerBindingCommandValidator"/> class.
    /// </summary>
    public DisableTriggerBindingCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

