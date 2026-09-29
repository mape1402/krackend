using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates metadata descriptor list query.
/// </summary>
public sealed class GetMetadataDescriptorsQueryValidator : AbstractValidator<GetMetadataDescriptorsQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetMetadataDescriptorsQueryValidator"/> class.
    /// </summary>
    public GetMetadataDescriptorsQueryValidator()
    {
        RuleFor(x => x.PagedSettings).NotNull();
        RuleFor(x => x.SearchText).MaximumLength(256);
    }
}
