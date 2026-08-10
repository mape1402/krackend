using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates update variable definition command inputs.
/// </summary>
public sealed class UpdateVariableDefinitionCommandValidator : AbstractValidator<UpdateVariableDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateVariableDefinitionCommandValidator"/> class.
    /// </summary>
    public UpdateVariableDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
    }
}

