using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates task compensation schema context queries.
/// </summary>
public sealed class GetTaskCompensationSchemaContextQueryValidator : AbstractValidator<GetTaskCompensationSchemaContextQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetTaskCompensationSchemaContextQueryValidator"/> class.
    /// </summary>
    public GetTaskCompensationSchemaContextQueryValidator()
    {
        RuleFor(x => x.OrchestrationVersionId).NotEmpty();
        RuleFor(x => x.TaskDefinitionId).NotEmpty();
    }
}

