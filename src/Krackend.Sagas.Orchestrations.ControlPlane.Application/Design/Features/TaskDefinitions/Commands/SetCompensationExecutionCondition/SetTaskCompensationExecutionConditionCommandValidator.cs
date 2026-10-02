using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates set task compensation execution condition commands.
/// </summary>
public sealed class SetTaskCompensationExecutionConditionCommandValidator : AbstractValidator<SetTaskCompensationExecutionConditionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetTaskCompensationExecutionConditionCommandValidator"/> class.
    /// </summary>
    public SetTaskCompensationExecutionConditionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

