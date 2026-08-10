using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Security.Interaction;

/// <summary>
/// Validates get teams query.
/// </summary>
public sealed class GetTeamsQueryValidator : AbstractValidator<GetTeamsQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetTeamsQueryValidator"/> class.
    /// </summary>
    public GetTeamsQueryValidator()
    {
        RuleFor(x => x.PagedSettings).NotNull();
        RuleFor(x => x.PagedSettings.PageNumber).GreaterThan(0);
        RuleFor(x => x.PagedSettings.PageSize).InclusiveBetween(1, 500);
        RuleFor(x => x.SearchText).MaximumLength(256);
    }
}
