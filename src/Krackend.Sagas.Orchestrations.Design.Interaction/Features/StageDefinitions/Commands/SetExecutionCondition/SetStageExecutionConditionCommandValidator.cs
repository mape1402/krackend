using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates set stage execution condition command inputs.
/// </summary>
public sealed class SetStageExecutionConditionCommandValidator : AbstractValidator<SetStageExecutionConditionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetStageExecutionConditionCommandValidator"/> class.
    /// </summary>
    public SetStageExecutionConditionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}
