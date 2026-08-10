using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates get orchestration versions query inputs.
/// </summary>
public sealed class GetOrchestrationVersionsQueryValidator : AbstractValidator<GetOrchestrationVersionsQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetOrchestrationVersionsQueryValidator"/> class.
    /// </summary>
    public GetOrchestrationVersionsQueryValidator()
    {
        RuleFor(x => x.OrchestrationDefinitionId).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Settings).NotNull();
        RuleFor(x => x.Settings.PageNumber).GreaterThan(0);
        RuleFor(x => x.Settings.PageSize).InclusiveBetween(1, 200);
    }
}


