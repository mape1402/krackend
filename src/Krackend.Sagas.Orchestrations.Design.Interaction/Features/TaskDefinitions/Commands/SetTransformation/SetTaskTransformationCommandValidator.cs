using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates set task transformation command inputs.
/// </summary>
public sealed class SetTaskTransformationCommandValidator : AbstractValidator<SetTaskTransformationCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetTaskTransformationCommandValidator"/> class.
    /// </summary>
    public SetTaskTransformationCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

