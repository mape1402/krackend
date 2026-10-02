using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates set trigger compensation transformation commands.
/// </summary>
public sealed class SetTriggerCompensationTransformationCommandValidator : AbstractValidator<SetTriggerCompensationTransformationCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetTriggerCompensationTransformationCommandValidator"/> class.
    /// </summary>
    public SetTriggerCompensationTransformationCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

