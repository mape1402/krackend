using FluentValidation;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates set task transformation command inputs.
/// </summary>
public sealed class SetTaskTransformationCommandValidator : AbstractValidator<SetTaskTransformationCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetTaskTransformationCommandValidator"/> class.
    /// </summary>
    public SetTaskTransformationCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Transformation).Must(IsSupportedTransformation);
    }

    private static bool IsSupportedTransformation(TransformationDefinition transformation)
        => transformation is null ||
            transformation.Engine == EngineType.DSL &&
            transformation.Configuration is DslTransformationConfiguration;
}

