using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates activate orchestration definition command inputs.
/// </summary>
public sealed class ActivateOrchestrationDefinitionCommandValidator : AbstractValidator<ActivateOrchestrationDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivateOrchestrationDefinitionCommandValidator"/> class.
    /// </summary>
    public ActivateOrchestrationDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}


