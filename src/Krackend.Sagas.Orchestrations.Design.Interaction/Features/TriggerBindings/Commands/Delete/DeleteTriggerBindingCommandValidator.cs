using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates delete trigger binding command inputs.
/// </summary>
public sealed class DeleteTriggerBindingCommandValidator : AbstractValidator<DeleteTriggerBindingCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteTriggerBindingCommandValidator"/> class.
    /// </summary>
    public DeleteTriggerBindingCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

