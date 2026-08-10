using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates get domains query.
/// </summary>
public sealed class GetDomainsQueryValidator : AbstractValidator<GetDomainsQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetDomainsQueryValidator"/> class.
    /// </summary>
    public GetDomainsQueryValidator()
    {
        RuleFor(x => x.PagedSettings).NotNull();
        RuleFor(x => x.SearchText).MaximumLength(256);
    }
}
