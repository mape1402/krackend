using FluentValidation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates get orchestration definitions query inputs.
/// </summary>
public sealed class GetOrchestrationDefinitionsQueryValidator : AbstractValidator<GetOrchestrationDefinitionsQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetOrchestrationDefinitionsQueryValidator"/> class.
    /// </summary>
    public GetOrchestrationDefinitionsQueryValidator()
    {
        RuleFor(x => x.Settings).NotNull();
        RuleFor(x => x.Settings.PageNumber).GreaterThan(0);
        RuleFor(x => x.Settings.PageSize).InclusiveBetween(1, 200);
    }
}


