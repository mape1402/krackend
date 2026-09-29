using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates metadata descriptor by id query.
/// </summary>
public sealed class GetMetadataDescriptorByIdQueryValidator : AbstractValidator<GetMetadataDescriptorByIdQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetMetadataDescriptorByIdQueryValidator"/> class.
    /// </summary>
    public GetMetadataDescriptorByIdQueryValidator()
    {
        RuleFor(x => x.MetadataDescriptorId)
            .NotEmpty()
            .Must(ValidationRules.IsUlid)
            .WithMessage("Metadata descriptor id must be a valid ULID.");
    }
}
