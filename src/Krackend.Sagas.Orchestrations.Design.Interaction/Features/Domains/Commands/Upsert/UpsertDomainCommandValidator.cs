using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates domain upsert command.
/// </summary>
public sealed class UpsertDomainCommandValidator : AbstractValidator<UpsertDomainCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpsertDomainCommandValidator"/> class.
    /// </summary>
    public UpsertDomainCommandValidator()
    {
        RuleFor(x => x.Id)
            .Must(value => string.IsNullOrWhiteSpace(value) || ValidationRules.IsUlid(value))
            .WithMessage("Id must be a valid ULID when provided.");
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Description).MaximumLength(2048);
    }
}
