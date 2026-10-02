using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates set trigger compensation execution condition commands.
/// </summary>
public sealed class SetTriggerCompensationExecutionConditionCommandValidator : AbstractValidator<SetTriggerCompensationExecutionConditionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetTriggerCompensationExecutionConditionCommandValidator"/> class.
    /// </summary>
    public SetTriggerCompensationExecutionConditionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

