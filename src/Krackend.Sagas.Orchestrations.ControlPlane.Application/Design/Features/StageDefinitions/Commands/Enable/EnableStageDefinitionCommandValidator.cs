using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents enable stage definition command validator.
/// </summary>
public sealed class EnableStageDefinitionCommandValidator : AbstractValidator<EnableStageDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnableStageDefinitionCommandValidator"/> class.
    /// </summary>
    public EnableStageDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
