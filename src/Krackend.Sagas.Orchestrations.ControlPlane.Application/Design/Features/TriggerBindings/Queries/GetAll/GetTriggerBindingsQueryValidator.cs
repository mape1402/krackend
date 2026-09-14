using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates get trigger bindings query inputs.
/// </summary>
public sealed class GetTriggerBindingsQueryValidator : AbstractValidator<GetTriggerBindingsQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetTriggerBindingsQueryValidator"/> class.
    /// </summary>
    public GetTriggerBindingsQueryValidator()
    {
        RuleFor(x => x.OrchestrationVersionId).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

