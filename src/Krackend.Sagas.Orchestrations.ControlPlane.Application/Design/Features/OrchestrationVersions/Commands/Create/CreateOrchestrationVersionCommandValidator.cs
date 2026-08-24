using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates create orchestration version command inputs.
/// </summary>
public sealed class CreateOrchestrationVersionCommandValidator : AbstractValidator<CreateOrchestrationVersionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateOrchestrationVersionCommandValidator"/> class.
    /// </summary>
    public CreateOrchestrationVersionCommandValidator()
    {
        RuleFor(x => x.OrchestrationDefinitionId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Version).NotEmpty().Must(ValidationRules.IsSemanticVersion);
        RuleFor(x => x.Checksum).NotEmpty();
        RuleFor(x => x.CreatedBy).NotEmpty().MaximumLength(128);
    }
}


