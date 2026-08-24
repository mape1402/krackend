using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

/// <summary>
/// Validates upsert team command.
/// </summary>
public sealed class UpsertTeamCommandValidator : AbstractValidator<UpsertTeamCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpsertTeamCommandValidator"/> class.
    /// </summary>
    public UpsertTeamCommandValidator()
    {
        RuleFor(x => x.TeamId)
            .Must(value => string.IsNullOrWhiteSpace(value) || ValidationRules.IsUlid(value))
            .WithMessage("TeamId must be a valid ULID when provided.");
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Description).MaximumLength(2048);
        RuleFor(x => x.Actor).NotEmpty().MaximumLength(128);
    }
}
