using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates metadata descriptor delete commands.
/// </summary>
public sealed class DeleteMetadataDescriptorCommandValidator : AbstractValidator<DeleteMetadataDescriptorCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteMetadataDescriptorCommandValidator"/> class.
    /// </summary>
    public DeleteMetadataDescriptorCommandValidator()
    {
        RuleFor(x => x.MetadataDescriptorId)
            .NotEmpty()
            .Must(ValidationRules.IsUlid)
            .WithMessage("Metadata descriptor id must be a valid ULID.");
    }
}
