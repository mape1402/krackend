namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Validates deployable artifacts against the messaging runtime capabilities.
/// </summary>
public sealed class MessagingRuntimeArtifactCompatibilityValidator : IRuntimeArtifactCompatibilityValidator
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public Task<RuntimeArtifactCompatibilityValidationResult> ValidateAsync(
        JsonNode artifactPayload,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (artifactPayload is null)
        {
            return Task.FromResult(RuntimeArtifactCompatibilityValidationResult.Failure(
                "ArtifactPayloadMissing",
                "Artifact payload is required for runtime compatibility validation."));
        }

        OrchestrationArtifact artifact;
        try
        {
            artifact = JsonSerializer.Deserialize<OrchestrationArtifact>(
                artifactPayload.ToJsonString(),
                SerializerOptions);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return Task.FromResult(RuntimeArtifactCompatibilityValidationResult.Failure(
                "ArtifactPayloadNotDeserializable",
                $"Artifact payload could not be deserialized as an orchestration artifact: {exception.Message}"));
        }

        if (artifact is null)
        {
            return Task.FromResult(RuntimeArtifactCompatibilityValidationResult.Failure(
                "ArtifactPayloadNotDeserializable",
                "Artifact payload could not be deserialized as an orchestration artifact."));
        }

        var result = ValidateArtifact(artifact);
        return Task.FromResult(result);
    }

    private static RuntimeArtifactCompatibilityValidationResult ValidateArtifact(OrchestrationArtifact artifact)
    {
        var stages = artifact.StageDefinitions?.OrderBy(stage => stage.Order).ToArray() ?? [];

        foreach (var trigger in artifact.TriggerBindings?.Where(trigger => trigger.IsEnabled) ?? Enumerable.Empty<TriggerBindingArtifact>())
        {
            if (trigger.TriggerType != TriggerType.Event ||
                trigger.TriggerChannel is not EventTriggerChannelArtifact)
            {
                return Failure(
                    "TriggerTransportNotSupported",
                    $"Trigger '{trigger.Id}' uses '{trigger.TriggerType}', but this runtime currently supports event messaging triggers.");
            }

            var eventChannel = (EventTriggerChannelArtifact)trigger.TriggerChannel;
            if (string.IsNullOrWhiteSpace(eventChannel.Topic))
            {
                return Failure(
                    "TriggerTopicMissing",
                    $"Trigger '{trigger.Id}' does not contain a messaging topic.");
            }

            var validationResult = ValidateValidation(
                eventChannel.Validation,
                eventChannel.SchemaBinding,
                $"trigger '{trigger.Id}'");
            if (!validationResult.Succeeded)
            {
                return validationResult;
            }
        }

        foreach (var stage in stages)
        {
            var conditionResult = ValidateCondition(stage.ExecutionCondition, $"stage '{stage.Key}'");
            if (!conditionResult.Succeeded)
            {
                return conditionResult;
            }

            var branchResult = ValidateBranches(stage, stages);
            if (!branchResult.Succeeded)
            {
                return branchResult;
            }

            foreach (var task in stage.TaskDefinitions?.Where(task => task.IsEnabled) ?? Enumerable.Empty<TaskArtifact>())
            {
                var taskResult = ValidateTask(task, stage);
                if (!taskResult.Succeeded)
                {
                    return taskResult;
                }
            }
        }

        return RuntimeArtifactCompatibilityValidationResult.Success();
    }

    private static RuntimeArtifactCompatibilityValidationResult ValidateTask(TaskArtifact task, StageArtifact stage)
    {
        if (task.Kind != TaskKind.Messaging)
        {
            return Failure(
                "TaskKindNotSupported",
                $"Task '{task.Key}' in stage '{stage.Key}' uses '{task.Kind}', but this runtime currently supports messaging tasks.");
        }

        if (task.Configuration is not MessagingTaskConfigurationArtifact messaging)
        {
            return Failure(
                "TaskConfigurationNotSupported",
                $"Task '{task.Key}' in stage '{stage.Key}' does not contain messaging task configuration.");
        }

        if (string.IsNullOrWhiteSpace(messaging.Topic))
        {
            return Failure(
                "MessagingTopicMissing",
                $"Task '{task.Key}' in stage '{stage.Key}' does not contain a messaging topic.");
        }

        if (task.DispatchType is not (TaskDispatchType.FireAndForget or TaskDispatchType.FireAndWaitCallback))
        {
            return Failure(
                "MessagingDispatchTypeNotSupported",
                $"Task '{task.Key}' in stage '{stage.Key}' uses '{task.DispatchType}', but messaging tasks must use FireAndForget or FireAndWaitCallback.");
        }

        var retryPolicyResult = ValidateRetryPolicy(task.RetryPolicy, $"task '{task.Key}'");
        if (!retryPolicyResult.Succeeded)
        {
            return retryPolicyResult;
        }

        var timeoutPolicyResult = ValidateTimeoutPolicy(task.TimeoutPolicy, $"task '{task.Key}'");
        if (!timeoutPolicyResult.Succeeded)
        {
            return timeoutPolicyResult;
        }

        var conditionResult = ValidateCondition(task.ExecutionCondition, $"task '{task.Key}'");
        if (!conditionResult.Succeeded)
        {
            return conditionResult;
        }

        var transformationResult = ValidateTransformation(task.Transformation, $"task '{task.Key}'");
        if (!transformationResult.Succeeded)
        {
            return transformationResult;
        }

        var requestValidationResult = ValidateValidation(
            messaging.RequestValidation,
            messaging.RequestSchemaBinding ?? messaging.SchemaBinding,
            $"task '{task.Key}' request");
        if (!requestValidationResult.Succeeded)
        {
            return requestValidationResult;
        }

        var responseValidationResult = ValidateValidation(
            messaging.ResponseValidation,
            messaging.ResponseSchemaBinding,
            $"task '{task.Key}' response");
        if (!responseValidationResult.Succeeded)
        {
            return responseValidationResult;
        }

        return ValidateCompensation(task, stage);
    }

    private static RuntimeArtifactCompatibilityValidationResult ValidateCompensation(TaskArtifact task, StageArtifact stage)
    {
        if (task.Compensation is null || task.Compensation.Configuration is null)
        {
            return RuntimeArtifactCompatibilityValidationResult.Success();
        }

        if (task.Compensation.CompensationTaskKind != TaskKind.Messaging)
        {
            return Failure(
                "CompensationTaskKindNotSupported",
                $"Compensation for task '{task.Key}' in stage '{stage.Key}' uses '{task.Compensation.CompensationTaskKind}', but this runtime currently supports messaging compensation tasks.");
        }

        if (task.Compensation.Configuration is not MessagingTaskConfigurationArtifact messaging)
        {
            return Failure(
                "CompensationConfigurationNotSupported",
                $"Compensation for task '{task.Key}' in stage '{stage.Key}' does not contain messaging task configuration.");
        }

        if (string.IsNullOrWhiteSpace(messaging.Topic))
        {
            return Failure(
                "CompensationMessagingTopicMissing",
                $"Compensation for task '{task.Key}' in stage '{stage.Key}' does not contain a messaging topic.");
        }

        if (task.Compensation.DispatchType != TaskDispatchType.FireAndForget)
        {
            return Failure(
                "CompensationDispatchTypeNotSupported",
                $"Compensation for task '{task.Key}' in stage '{stage.Key}' uses '{task.Compensation.DispatchType}', but messaging compensations currently run as FireAndForget.");
        }

        var retryPolicyResult = ValidateRetryPolicy(task.Compensation.RetryPolicy, $"compensation for task '{task.Key}'");
        if (!retryPolicyResult.Succeeded)
        {
            return retryPolicyResult;
        }

        var timeoutPolicyResult = ValidateTimeoutPolicy(task.Compensation.TimeoutPolicy, $"compensation for task '{task.Key}'");
        if (!timeoutPolicyResult.Succeeded)
        {
            return timeoutPolicyResult;
        }

        var conditionResult = ValidateCondition(task.Compensation.ExecutionCondition, $"compensation for task '{task.Key}'");
        if (!conditionResult.Succeeded)
        {
            return conditionResult;
        }

        return ValidateTransformation(task.Compensation.Transformation, $"compensation for task '{task.Key}'");
    }

    private static RuntimeArtifactCompatibilityValidationResult ValidateBranches(
        StageArtifact stage,
        IReadOnlyCollection<StageArtifact> stages)
    {
        foreach (var rule in stage.BranchRules ?? [])
        {
            if (rule.FromType != ElementType.Stage)
            {
                return Failure(
                    "BranchSourceTypeNotSupported",
                    $"Branch rule '{rule.Id}' in stage '{stage.Key}' starts from '{rule.FromType}', but this runtime currently supports stage-level branch rules.");
            }

            if (rule.NavigateToType != ElementType.Stage)
            {
                return Failure(
                    "BranchTargetTypeNotSupported",
                    $"Branch rule '{rule.Id}' in stage '{stage.Key}' navigates to '{rule.NavigateToType}', but this runtime currently supports stage targets.");
            }

            var target = stages.FirstOrDefault(candidate => candidate.Id == rule.NavigateToId);
            if (target is null)
            {
                return Failure(
                    "BranchTargetNotFound",
                    $"Branch rule '{rule.Id}' in stage '{stage.Key}' targets stage '{rule.NavigateToId}', but that stage is not present in the artifact.");
            }

            if (target.Order <= stage.Order)
            {
                return Failure(
                    "BranchTargetOrderNotSupported",
                    $"Branch rule '{rule.Id}' in stage '{stage.Key}' targets stage '{target.Key}', but this runtime currently supports forward stage navigation.");
            }

            var conditionResult = ValidateCondition(rule.Condition, $"branch rule '{rule.Id}'");
            if (!conditionResult.Succeeded)
            {
                return conditionResult;
            }
        }

        return RuntimeArtifactCompatibilityValidationResult.Success();
    }

    private static RuntimeArtifactCompatibilityValidationResult ValidateRetryPolicy(
        RetryPolicyArtifact retryPolicy,
        string owner)
    {
        if (retryPolicy is null)
        {
            return RuntimeArtifactCompatibilityValidationResult.Success();
        }

        if (retryPolicy.MaxRetries < 0)
        {
            return Failure(
                "RetryMaxRetriesInvalid",
                $"The retry policy configured for {owner} uses a negative max retry count.");
        }

        if (retryPolicy.StrategyType != RetryStrategyType.Fixed ||
            retryPolicy.Strategy is not FixedRetryStrategyArtifact fixedRetry ||
            fixedRetry.Delay.Value < TimeSpan.Zero)
        {
            return Failure(
                "RetryStrategyNotSupported",
                $"The retry policy configured for {owner} uses '{retryPolicy.StrategyType}', but this runtime currently supports Fixed retry strategy.");
        }

        return RuntimeArtifactCompatibilityValidationResult.Success();
    }

    private static RuntimeArtifactCompatibilityValidationResult ValidateTimeoutPolicy(
        TimeoutPolicyArtifact timeoutPolicy,
        string owner)
    {
        if (timeoutPolicy is null)
        {
            return RuntimeArtifactCompatibilityValidationResult.Success();
        }

        if (timeoutPolicy.Timeout.Value <= TimeSpan.Zero)
        {
            return Failure(
                "TimeoutDurationInvalid",
                $"The timeout policy configured for {owner} must use a positive timeout duration.");
        }

        if (timeoutPolicy.TimeoutBehaviorPolicy is null ||
            timeoutPolicy.TimeoutBehaviorPolicy.Behavior != timeoutPolicy.TimeoutBehavior)
        {
            return Failure(
                "TimeoutBehaviorMismatch",
                $"The timeout policy configured for {owner} does not match the selected timeout behavior.");
        }

        if (timeoutPolicy.TimeoutBehaviorPolicy is ReconcileTimeoutBehaviorPolicyArtifact reconcile)
        {
            return ValidateRetryPolicy(reconcile.RetryPolicy, $"{owner} timeout reconciliation");
        }

        if (timeoutPolicy.TimeoutBehaviorPolicy is WaitTimeoutBehaviorPolicyArtifact wait &&
            wait.WaitingTime.Value <= TimeSpan.Zero)
        {
            return Failure(
                "TimeoutWaitDurationInvalid",
                $"The wait timeout policy configured for {owner} must use a positive wait duration.");
        }

        if (timeoutPolicy.TimeoutBehaviorPolicy is FailTimeoutBehaviorPolicyArtifact or WaitTimeoutBehaviorPolicyArtifact)
        {
            return RuntimeArtifactCompatibilityValidationResult.Success();
        }

        return Failure(
            "TimeoutBehaviorNotSupported",
            $"The timeout policy configured for {owner} uses '{timeoutPolicy.TimeoutBehavior}', but this runtime cannot interpret its behavior payload.");
    }

    private static RuntimeArtifactCompatibilityValidationResult ValidateCondition(
        ExecutionConditionArtifact condition,
        string owner)
    {
        if (condition?.IsEnabled != true)
        {
            return RuntimeArtifactCompatibilityValidationResult.Success();
        }

        if (condition.Engine != EngineType.DSL ||
            condition.Configuration is not DslConditionConfigurationArtifact dsl)
        {
            return Failure(
                "ConditionEngineNotSupported",
                $"The condition configured for {owner} uses '{condition.Engine}', but this runtime currently supports DSL conditions.");
        }

        if (string.IsNullOrWhiteSpace(dsl.Expression.ToString()))
        {
            return Failure(
                "ConditionExpressionMissing",
                $"The condition configured for {owner} is enabled but does not contain an expression.");
        }

        return RuntimeArtifactCompatibilityValidationResult.Success();
    }

    private static RuntimeArtifactCompatibilityValidationResult ValidateTransformation(
        TransformationArtifact transformation,
        string owner)
    {
        if (transformation?.IsEnabled != true)
        {
            return RuntimeArtifactCompatibilityValidationResult.Success();
        }

        if (transformation.Engine != EngineType.DSL ||
            transformation.Configuration is not DslTransformationConfigurationArtifact dsl)
        {
            return Failure(
                "TransformationEngineNotSupported",
                $"The transformation configured for {owner} uses '{transformation.Engine}', but this runtime currently supports DSL transformations.");
        }

        if (string.IsNullOrWhiteSpace(dsl.Dsl))
        {
            return Failure(
                "TransformationDslMissing",
                $"The transformation configured for {owner} is enabled but does not contain executable DSL.");
        }

        return RuntimeArtifactCompatibilityValidationResult.Success();
    }

    private static RuntimeArtifactCompatibilityValidationResult ValidateValidation(
        ValidationArtifact validation,
        SchemaBindingArtifact schemaBinding,
        string owner)
    {
        if (validation?.IsEnabled != true &&
            schemaBinding?.IsValidationEnabled != true)
        {
            return RuntimeArtifactCompatibilityValidationResult.Success();
        }

        if (validation is null)
        {
            return Failure(
                "ValidationDslMissing",
                $"The validation configured for {owner} is enabled by schema binding but does not contain an executable validation contract.");
        }

        if (validation.Engine != EngineType.DSL ||
            validation.Configuration is not DslValidationConfigurationArtifact dsl)
        {
            return Failure(
                "ValidationEngineNotSupported",
                $"The validation configured for {owner} uses '{validation.Engine}', but this runtime currently supports DSL validations.");
        }

        if (string.IsNullOrWhiteSpace(dsl.Dsl))
        {
            return Failure(
                "ValidationDslMissing",
                $"The validation configured for {owner} is enabled but does not contain executable DSL.");
        }

        return RuntimeArtifactCompatibilityValidationResult.Success();
    }

    private static RuntimeArtifactCompatibilityValidationResult Failure(string errorCode, string errorMessage)
        => RuntimeArtifactCompatibilityValidationResult.Failure(errorCode, errorMessage);
}
