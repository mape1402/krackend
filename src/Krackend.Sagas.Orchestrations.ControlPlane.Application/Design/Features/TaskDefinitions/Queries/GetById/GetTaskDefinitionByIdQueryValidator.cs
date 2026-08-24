using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates get task definition by id query inputs.
/// </summary>
public sealed class GetTaskDefinitionByIdQueryValidator : AbstractValidator<GetTaskDefinitionByIdQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetTaskDefinitionByIdQueryValidator"/> class.
    /// </summary>
    public GetTaskDefinitionByIdQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

