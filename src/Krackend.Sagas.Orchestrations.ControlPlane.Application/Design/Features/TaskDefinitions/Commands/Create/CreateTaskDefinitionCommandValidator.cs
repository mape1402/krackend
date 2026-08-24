using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates create task definition command inputs.
/// </summary>
public sealed class CreateTaskDefinitionCommandValidator : AbstractValidator<CreateTaskDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTaskDefinitionCommandValidator"/> class.
    /// </summary>
    public CreateTaskDefinitionCommandValidator()
    {
        RuleFor(x => x.StageDefinitionId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Order).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ParallelGroupId).Must(x => string.IsNullOrWhiteSpace(x) || ValidationRules.IsUlid(x));
        RuleFor(x => x.Configuration).NotNull();
    }
}

