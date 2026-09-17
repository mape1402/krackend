using FluentValidation;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.RetryStrategies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TimeoutBehaviorPolicies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ValidationConfigurations;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Validates update task definition command inputs.
/// </summary>
public sealed class UpdateTaskDefinitionCommandValidator : AbstractValidator<UpdateTaskDefinitionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTaskDefinitionCommandValidator"/> class.
    /// </summary>
    public UpdateTaskDefinitionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Must(ValidationRules.IsUlid);
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Order).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ParallelGroupId).Must(x => string.IsNullOrWhiteSpace(x) || ValidationRules.IsUlid(x));
        RuleFor(x => x.Kind).Equal(TaskKind.Messaging);
        RuleFor(x => x.DispatchType).Must(IsSupportedDispatchType);
        RuleFor(x => x.Configuration).NotNull();
        RuleFor(x => x.Configuration).Must(IsSupportedMessagingConfiguration);
        RuleFor(x => x.ExecutionCondition).Must(IsSupportedCondition);
        RuleFor(x => x.Transformation).Must(IsSupportedTransformation);
        RuleFor(x => x.RetryPolicy).Must(IsSupportedRetryPolicy);
        RuleFor(x => x.TimeoutPolicy).Must(IsSupportedTimeoutPolicy);
        RuleFor(x => x.CompensationDefinition).Must(IsSupportedCompensation);
    }

    private static bool IsSupportedDispatchType(TaskDispatchType dispatchType)
        => dispatchType is TaskDispatchType.FireAndForget or TaskDispatchType.FireAndWaitCallback;

    private static bool IsSupportedMessagingConfiguration(ITaskConfiguration configuration)
    {
        if (configuration is not MessagingTaskConfiguration messaging ||
            string.IsNullOrWhiteSpace(messaging.Topic))
        {
            return false;
        }

        return IsSupportedValidation(messaging.RequestValidation, IsRequestValidationEnabled(messaging)) &&
            IsSupportedValidation(messaging.ResponseValidation, IsResponseValidationEnabled(messaging));
    }

    private static bool IsSupportedCondition(ExecutionCondition condition)
        => condition is null ||
            condition.Engine == EngineType.DSL &&
            condition.Configuration is DslConditionConfiguration;

    private static bool IsSupportedTransformation(TransformationDefinition transformation)
        => transformation is null ||
            transformation.Engine == EngineType.DSL &&
            transformation.Configuration is DslTransformationConfiguration;

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

    private static bool IsSupportedRetryPolicy(RetryPolicy retryPolicy)
        => retryPolicy is null ||
            retryPolicy.MaxRetries >= 0 &&
            retryPolicy.StrategyType == RetryStrategyType.Fixed &&
            retryPolicy.Strategy is FixedRetryStrategy fixedRetry &&
            fixedRetry.Delay.Value >= TimeSpan.Zero;

    private static bool IsSupportedTimeoutPolicy(TimeoutPolicy timeoutPolicy)
    {
        if (timeoutPolicy is null)
        {
            return true;
        }

        if (timeoutPolicy.Timeout.Value <= TimeSpan.Zero ||
            timeoutPolicy.TimeoutBehaviorPolicy is null ||
            timeoutPolicy.TimeoutBehaviorPolicy.Behavior != timeoutPolicy.TimeoutBehavior)
        {
            return false;
        }

        return timeoutPolicy.TimeoutBehaviorPolicy switch
        {
            FailTimeoutBehaviorPolicy => true,
            WaitTimeoutBehaviorPolicy wait => wait.WaitingTime.Value > TimeSpan.Zero,
            ReconcileTimeoutBehaviorPolicy reconcile => IsSupportedRetryPolicy(reconcile.RetryPolicy),
            _ => false
        };
    }

    private static bool IsSupportedCompensation(CompensationDefinition compensation)
        => compensation is null ||
            compensation.CompensationTaskKind == TaskKind.Messaging &&
            compensation.DispatchType == TaskDispatchType.FireAndForget &&
            IsSupportedMessagingConfiguration(compensation.Configuration) &&
            IsSupportedCondition(compensation.ExecutionCondition) &&
            IsSupportedTransformation(compensation.Transformation) &&
            IsSupportedRetryPolicy(compensation.RetryPolicy) &&
            IsSupportedTimeoutPolicy(compensation.TimeoutPolicy);

    private static bool IsRequestValidationEnabled(MessagingTaskConfiguration configuration)
        => configuration.HasRequestValidation || configuration.HasSchemaValidation;

    private static bool IsResponseValidationEnabled(MessagingTaskConfiguration configuration)
        => configuration.HasResponseValidation || configuration.ResponseSchemaBinding?.IsValidationEnabled == true;
}
