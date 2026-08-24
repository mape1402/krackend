using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates delete variable definition command inputs.
/// </summary>
public sealed class DeleteVariableDefinitionCommandValidator : AbstractValidator<DeleteVariableDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteVariableDefinitionCommandValidator"/> class.
    /// </summary>
    public DeleteVariableDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

