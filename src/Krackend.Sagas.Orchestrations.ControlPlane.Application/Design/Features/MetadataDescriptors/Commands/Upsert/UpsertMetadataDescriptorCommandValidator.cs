using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates metadata descriptor upsert command.
/// </summary>
public sealed class UpsertMetadataDescriptorCommandValidator : AbstractValidator<UpsertMetadataDescriptorCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpsertMetadataDescriptorCommandValidator"/> class.
    /// </summary>
    public UpsertMetadataDescriptorCommandValidator()
    {
        RuleFor(x => x.Id)
            .Must(value => string.IsNullOrWhiteSpace(value) || ValidationRules.IsUlid(value))
            .WithMessage("Id must be a valid ULID when provided.");
        RuleFor(x => x.Key)
            .NotEmpty()
            .MaximumLength(128)
            .Matches("^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$")
            .WithMessage("Use lowercase segments separated only by underscores, starting with a letter.");
        RuleFor(x => x.SourceKey)
            .MaximumLength(256)
            .Must(value => string.IsNullOrWhiteSpace(value) || value.All(character => !char.IsControl(character)))
            .WithMessage("Source key cannot contain control characters.");
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Description).MaximumLength(2048);
        RuleFor(x => x.SchemaJson)
            .NotEmpty()
            .Must(MetadataDescriptorSchemaHasher.IsJsonObject)
            .WithMessage("Schema must be a valid JSON object.");
    }
}
