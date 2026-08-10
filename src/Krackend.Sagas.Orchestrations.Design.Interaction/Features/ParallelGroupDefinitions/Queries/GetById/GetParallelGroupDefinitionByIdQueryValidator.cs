using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates get parallel group definition by id query inputs.
/// </summary>
public sealed class GetParallelGroupDefinitionByIdQueryValidator : AbstractValidator<GetParallelGroupDefinitionByIdQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetParallelGroupDefinitionByIdQueryValidator"/> class.
    /// </summary>
    public GetParallelGroupDefinitionByIdQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

