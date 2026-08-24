using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates domain get by id query.
/// </summary>
public sealed class GetDomainByIdQueryValidator : AbstractValidator<GetDomainByIdQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetDomainByIdQueryValidator"/> class.
    /// </summary>
    public GetDomainByIdQueryValidator()
    {
        RuleFor(x => x.DomainId).NotEmpty().Must(ValidationRules.IsUlid);
    }
}
