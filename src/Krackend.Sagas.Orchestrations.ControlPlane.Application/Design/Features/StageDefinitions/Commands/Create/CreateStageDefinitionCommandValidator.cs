using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates create stage definition command inputs.
/// </summary>
public sealed class CreateStageDefinitionCommandValidator : AbstractValidator<CreateStageDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateStageDefinitionCommandValidator"/> class.
    /// </summary>
    public CreateStageDefinitionCommandValidator()
    {
        RuleFor(x => x.OrchestrationVersionId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Order).GreaterThanOrEqualTo(0);
    }
}

