using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates trigger compensation schema context queries.
/// </summary>
public sealed class GetTriggerCompensationSchemaContextQueryValidator : AbstractValidator<GetTriggerCompensationSchemaContextQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetTriggerCompensationSchemaContextQueryValidator"/> class.
    /// </summary>
    public GetTriggerCompensationSchemaContextQueryValidator()
    {
        RuleFor(x => x.OrchestrationVersionId).NotEmpty();
        RuleFor(x => x.TriggerBindingId).NotEmpty();
    }
}

