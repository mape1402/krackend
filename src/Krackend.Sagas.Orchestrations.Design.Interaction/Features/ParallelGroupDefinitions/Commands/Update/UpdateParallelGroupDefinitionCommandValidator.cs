using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates update parallel group definition command inputs.
/// </summary>
public sealed class UpdateParallelGroupDefinitionCommandValidator : AbstractValidator<UpdateParallelGroupDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateParallelGroupDefinitionCommandValidator"/> class.
    /// </summary>
    public UpdateParallelGroupDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.MaxParallelAgents).GreaterThan(0).When(x => x.MaxParallelAgents.HasValue);
    }
}

