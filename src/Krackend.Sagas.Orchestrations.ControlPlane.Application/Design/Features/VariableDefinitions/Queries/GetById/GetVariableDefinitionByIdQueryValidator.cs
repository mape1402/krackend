using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates get variable definition by id query inputs.
/// </summary>
public sealed class GetVariableDefinitionByIdQueryValidator : AbstractValidator<GetVariableDefinitionByIdQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetVariableDefinitionByIdQueryValidator"/> class.
    /// </summary>
    public GetVariableDefinitionByIdQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

