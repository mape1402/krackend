using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates create trigger binding command inputs.
/// </summary>
public sealed class CreateTriggerBindingCommandValidator : AbstractValidator<CreateTriggerBindingCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTriggerBindingCommandValidator"/> class.
    /// </summary>
    public CreateTriggerBindingCommandValidator()
    {
        RuleFor(x => x.OrchestrationVersionId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.TriggerChannel).NotNull();
    }
}

