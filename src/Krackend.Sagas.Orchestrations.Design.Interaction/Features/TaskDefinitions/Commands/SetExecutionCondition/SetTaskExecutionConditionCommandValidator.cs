using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates set task execution condition command inputs.
/// </summary>
public sealed class SetTaskExecutionConditionCommandValidator : AbstractValidator<SetTaskExecutionConditionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetTaskExecutionConditionCommandValidator"/> class.
    /// </summary>
    public SetTaskExecutionConditionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}
