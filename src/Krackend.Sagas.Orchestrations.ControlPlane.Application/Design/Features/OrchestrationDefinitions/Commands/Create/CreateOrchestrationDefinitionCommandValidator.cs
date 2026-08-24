using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates create orchestration definition command inputs.
/// </summary>
public sealed class CreateOrchestrationDefinitionCommandValidator : AbstractValidator<CreateOrchestrationDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateOrchestrationDefinitionCommandValidator"/> class.
    /// </summary>
    public CreateOrchestrationDefinitionCommandValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.DomainId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.OwnerTeamId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.CreatedBy).NotEmpty().MaximumLength(128);
        RuleForEach(x => x.Tags).MaximumLength(64);
    }
}


