using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates disable task definition command inputs.
/// </summary>
public sealed class DisableTaskDefinitionCommandValidator : AbstractValidator<DisableTaskDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisableTaskDefinitionCommandValidator"/> class.
    /// </summary>
    public DisableTaskDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

