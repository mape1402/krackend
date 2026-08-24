using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates create variable definition command inputs.
/// </summary>
public sealed class CreateVariableDefinitionCommandValidator : AbstractValidator<CreateVariableDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateVariableDefinitionCommandValidator"/> class.
    /// </summary>
    public CreateVariableDefinitionCommandValidator()
    {
        RuleFor(x => x.OrchestrationVersionId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
    }
}

