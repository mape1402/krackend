using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates update orchestration definition command inputs.
/// </summary>
public sealed class UpdateOrchestrationDefinitionCommandValidator : AbstractValidator<UpdateOrchestrationDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOrchestrationDefinitionCommandValidator"/> class.
    /// </summary>
    public UpdateOrchestrationDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.DomainId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.OwnerTeamId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.UpdatedBy).NotEmpty().MaximumLength(128);
        RuleForEach(x => x.Tags).MaximumLength(64);
    }
}


