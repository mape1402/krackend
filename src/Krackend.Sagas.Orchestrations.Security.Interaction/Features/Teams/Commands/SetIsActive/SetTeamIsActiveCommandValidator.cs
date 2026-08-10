using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Security.Interaction;

/// <summary>
/// Validates team active state command.
/// </summary>
public sealed class SetTeamIsActiveCommandValidator : AbstractValidator<SetTeamIsActiveCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetTeamIsActiveCommandValidator"/> class.
    /// </summary>
    public SetTeamIsActiveCommandValidator()
    {
        RuleFor(x => x.TeamId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Actor).NotEmpty().MaximumLength(128);
    }
}
