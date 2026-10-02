using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates set task compensation transformation commands.
/// </summary>
public sealed class SetTaskCompensationTransformationCommandValidator : AbstractValidator<SetTaskCompensationTransformationCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetTaskCompensationTransformationCommandValidator"/> class.
    /// </summary>
    public SetTaskCompensationTransformationCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

