using FluentValidation;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ValidationConfigurations;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates task entry validation update requests.
/// </summary>
public sealed class SetTaskEntryValidationCommandValidator : AbstractValidator<SetTaskEntryValidationCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetTaskEntryValidationCommandValidator"/> class.
    /// </summary>
    public SetTaskEntryValidationCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.EntryValidation).Must(IsSupportedValidation);
    }

    private static bool IsSupportedValidation(ValidationDefinition validation)
        => validation is null ||
            validation.Engine == EngineType.DSL &&
            validation.Configuration is DslValidationConfiguration;
}
