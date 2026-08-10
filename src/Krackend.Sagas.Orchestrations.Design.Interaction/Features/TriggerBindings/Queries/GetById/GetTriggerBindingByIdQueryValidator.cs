using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates get trigger binding by id query inputs.
/// </summary>
public sealed class GetTriggerBindingByIdQueryValidator : AbstractValidator<GetTriggerBindingByIdQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetTriggerBindingByIdQueryValidator"/> class.
    /// </summary>
    public GetTriggerBindingByIdQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}

