using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

/// <summary>
/// Validates remove team member command.
/// </summary>
public sealed class RemoveTeamMemberCommandValidator : AbstractValidator<RemoveTeamMemberCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveTeamMemberCommandValidator"/> class.
    /// </summary>
    public RemoveTeamMemberCommandValidator()
    {
        RuleFor(x => x.TeamId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.ExternalUserId).NotEmpty().MaximumLength(256);
    }
}
