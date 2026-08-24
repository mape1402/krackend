using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

/// <summary>
/// Validates add team member command.
/// </summary>
public sealed class AddTeamMemberCommandValidator : AbstractValidator<AddTeamMemberCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddTeamMemberCommandValidator"/> class.
    /// </summary>
    public AddTeamMemberCommandValidator()
    {
        RuleFor(x => x.TeamId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.ExternalUserId).NotEmpty().MaximumLength(256);
        RuleFor(x => x.DisplayName).MaximumLength(256);
    }
}
