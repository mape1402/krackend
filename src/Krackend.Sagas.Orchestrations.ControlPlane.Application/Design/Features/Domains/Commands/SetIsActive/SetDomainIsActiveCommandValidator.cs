using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates set active command for domains.
/// </summary>
public sealed class SetDomainIsActiveCommandValidator : AbstractValidator<SetDomainIsActiveCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetDomainIsActiveCommandValidator"/> class.
    /// </summary>
    public SetDomainIsActiveCommandValidator()
    {
        RuleFor(x => x.DomainId).NotEmpty().Must(ValidationRules.IsUlid);
    }
}
