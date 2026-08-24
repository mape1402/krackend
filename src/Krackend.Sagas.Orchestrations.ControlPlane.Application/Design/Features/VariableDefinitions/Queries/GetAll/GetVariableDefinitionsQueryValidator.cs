using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates get variable definitions query inputs.
/// </summary>
public sealed class GetVariableDefinitionsQueryValidator : AbstractValidator<GetVariableDefinitionsQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetVariableDefinitionsQueryValidator"/> class.
    /// </summary>
    public GetVariableDefinitionsQueryValidator()
    {
        RuleFor(x => x.OrchestrationVersionId).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

