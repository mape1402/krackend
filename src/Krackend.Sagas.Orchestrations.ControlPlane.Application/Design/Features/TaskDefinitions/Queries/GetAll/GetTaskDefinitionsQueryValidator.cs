using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates get task definitions query inputs.
/// </summary>
public sealed class GetTaskDefinitionsQueryValidator : AbstractValidator<GetTaskDefinitionsQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetTaskDefinitionsQueryValidator"/> class.
    /// </summary>
    public GetTaskDefinitionsQueryValidator()
    {
        RuleFor(x => x.StageDefinitionId).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

