using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates enable trigger binding command inputs.
/// </summary>
public sealed class EnableTriggerBindingCommandValidator : AbstractValidator<EnableTriggerBindingCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnableTriggerBindingCommandValidator"/> class.
    /// </summary>
    public EnableTriggerBindingCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

