using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents disable stage definition command validator.
/// </summary>
public sealed class DisableStageDefinitionCommandValidator : AbstractValidator<DisableStageDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisableStageDefinitionCommandValidator"/> class.
    /// </summary>
    public DisableStageDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
