using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates update task definition command inputs.
/// </summary>
public sealed class UpdateTaskDefinitionCommandValidator : AbstractValidator<UpdateTaskDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTaskDefinitionCommandValidator"/> class.
    /// </summary>
    public UpdateTaskDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Order).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ParallelGroupId).Must(x => string.IsNullOrWhiteSpace(x) || ValidationRules.IsUlid(x));
        RuleFor(x => x.Configuration).NotNull();
    }
}

