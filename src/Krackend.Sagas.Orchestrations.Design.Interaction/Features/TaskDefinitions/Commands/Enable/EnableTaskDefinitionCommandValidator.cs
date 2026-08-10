using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates enable task definition command inputs.
/// </summary>
public sealed class EnableTaskDefinitionCommandValidator : AbstractValidator<EnableTaskDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnableTaskDefinitionCommandValidator"/> class.
    /// </summary>
    public EnableTaskDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

