using FluentValidation;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Validates return orchestration version to draft command inputs.
/// </summary>
public sealed class ReturnOrchestrationVersionToDraftCommandValidator : AbstractValidator<ReturnOrchestrationVersionToDraftCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReturnOrchestrationVersionToDraftCommandValidator"/> class.
    /// </summary>
    public ReturnOrchestrationVersionToDraftCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
    }
}


