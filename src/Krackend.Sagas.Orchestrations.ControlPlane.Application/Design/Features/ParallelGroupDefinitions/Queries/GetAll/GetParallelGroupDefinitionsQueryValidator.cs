using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates get parallel group definitions query inputs.
/// </summary>
public sealed class GetParallelGroupDefinitionsQueryValidator : AbstractValidator<GetParallelGroupDefinitionsQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetParallelGroupDefinitionsQueryValidator"/> class.
    /// </summary>
    public GetParallelGroupDefinitionsQueryValidator()
    {
        RuleFor(x => x.StageDefinitionId).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

