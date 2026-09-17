using FluentValidation;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ValidationConfigurations;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates update trigger binding command inputs.
/// </summary>
public sealed class UpdateTriggerBindingCommandValidator : AbstractValidator<UpdateTriggerBindingCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTriggerBindingCommandValidator"/> class.
    /// </summary>
    public UpdateTriggerBindingCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.TriggerType).Equal(TriggerType.Event);
        RuleFor(x => x.TriggerChannel).NotNull();
        RuleFor(x => x.TriggerChannel).Must(IsSupportedEventChannel);
    }

    private static bool IsSupportedEventChannel(ITriggerChannel channel)
        => channel is EventTriggerChannel eventChannel &&
            !string.IsNullOrWhiteSpace(eventChannel.Topic) &&
            IsSupportedValidation(eventChannel.Validation, eventChannel.HasValidation || eventChannel.HasSchemaValidation);

    private static bool IsSupportedValidation(ValidationDefinition validation, bool isEnabled)
    {
        if (validation is null)
        {
            return !isEnabled;
        }

        if (validation.Engine != EngineType.DSL ||
            validation.Configuration is not DslValidationConfiguration dsl)
        {
            return false;
        }

        return !isEnabled || !string.IsNullOrWhiteSpace(dsl.Dsl);
    }
}

