using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Security.Interaction;

/// <summary>
/// Validates get team members query.
/// </summary>
public sealed class GetTeamMembersQueryValidator : AbstractValidator<GetTeamMembersQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetTeamMembersQueryValidator"/> class.
    /// </summary>
    public GetTeamMembersQueryValidator()
    {
        RuleFor(x => x.TeamId).NotEmpty().Must(ValidationRules.IsUlid);
    }
}
