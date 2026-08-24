using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates create parallel group definition command inputs.
/// </summary>
public sealed class CreateParallelGroupDefinitionCommandValidator : AbstractValidator<CreateParallelGroupDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateParallelGroupDefinitionCommandValidator"/> class.
    /// </summary>
    public CreateParallelGroupDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).Must(x => string.IsNullOrWhiteSpace(x) || ValidationRules.IsUlid(x));
        RuleFor(x => x.StageDefinitionId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.MaxParallelAgents).GreaterThan(0).When(x => x.MaxParallelAgents.HasValue);
    }
}

