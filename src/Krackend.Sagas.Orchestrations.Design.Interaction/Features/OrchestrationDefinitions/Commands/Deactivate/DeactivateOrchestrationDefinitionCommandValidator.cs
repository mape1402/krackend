using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates deactivate orchestration definition command inputs.
/// </summary>
public sealed class DeactivateOrchestrationDefinitionCommandValidator : AbstractValidator<DeactivateOrchestrationDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeactivateOrchestrationDefinitionCommandValidator"/> class.
    /// </summary>
    public DeactivateOrchestrationDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}


