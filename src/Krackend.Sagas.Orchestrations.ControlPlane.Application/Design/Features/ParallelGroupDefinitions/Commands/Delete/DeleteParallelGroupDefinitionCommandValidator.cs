using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates delete parallel group definition command inputs.
/// </summary>
public sealed class DeleteParallelGroupDefinitionCommandValidator : AbstractValidator<DeleteParallelGroupDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteParallelGroupDefinitionCommandValidator"/> class.
    /// </summary>
    public DeleteParallelGroupDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

