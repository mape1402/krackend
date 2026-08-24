using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates delete stage definition command inputs.
/// </summary>
public sealed class DeleteStageDefinitionCommandValidator : AbstractValidator<DeleteStageDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteStageDefinitionCommandValidator"/> class.
    /// </summary>
    public DeleteStageDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

