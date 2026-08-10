using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates delete task definition command inputs.
/// </summary>
public sealed class DeleteTaskDefinitionCommandValidator : AbstractValidator<DeleteTaskDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteTaskDefinitionCommandValidator"/> class.
    /// </summary>
    public DeleteTaskDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

