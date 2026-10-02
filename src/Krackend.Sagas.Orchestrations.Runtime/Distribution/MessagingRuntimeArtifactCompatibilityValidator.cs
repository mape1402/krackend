namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Extensions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Extensions;

/// <summary>
/// Validates deployable artifacts against configured runtime task adapter capabilities.
/// </summary>
public sealed class MessagingRuntimeArtifactCompatibilityValidator : IRuntimeArtifactCompatibilityValidator
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ITaskRuntimeAdapterRegistry _adapterRegistry;
    private readonly IOrchestrationArtifactMigrator _artifactMigrator;
    private readonly IRuntimeExtensionPackageRepository _extensionPackageRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingRuntimeArtifactCompatibilityValidator"/> class.
    /// </summary>
    public MessagingRuntimeArtifactCompatibilityValidator()
        : this(new TaskRuntimeAdapterRegistry([new MessagingTaskRuntimeAdapter()]), new DefaultOrchestrationArtifactMigrator())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingRuntimeArtifactCompatibilityValidator"/> class.
    /// </summary>
    /// <param name="adapterRegistry">Runtime task adapter registry.</param>
    public MessagingRuntimeArtifactCompatibilityValidator(ITaskRuntimeAdapterRegistry adapterRegistry)
        : this(adapterRegistry, new DefaultOrchestrationArtifactMigrator())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingRuntimeArtifactCompatibilityValidator"/> class.
    /// </summary>
    /// <param name="adapterRegistry">Runtime task adapter registry.</param>
    /// <param name="artifactMigrator">Artifact migrator.</param>
    public MessagingRuntimeArtifactCompatibilityValidator(
        ITaskRuntimeAdapterRegistry adapterRegistry,
        IOrchestrationArtifactMigrator artifactMigrator)
        : this(adapterRegistry, artifactMigrator, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingRuntimeArtifactCompatibilityValidator"/> class.
    /// </summary>
    /// <param name="adapterRegistry">Runtime task adapter registry.</param>
    /// <param name="artifactMigrator">Artifact migrator.</param>
    /// <param name="extensionPackageRepository">Runtime extension package repository.</param>
    public MessagingRuntimeArtifactCompatibilityValidator(
        ITaskRuntimeAdapterRegistry adapterRegistry,
        IOrchestrationArtifactMigrator artifactMigrator,
        IRuntimeExtensionPackageRepository extensionPackageRepository)
    {
        _adapterRegistry = adapterRegistry ?? throw new ArgumentNullException(nameof(adapterRegistry));
        _artifactMigrator = artifactMigrator ?? throw new ArgumentNullException(nameof(artifactMigrator));
        _extensionPackageRepository = extensionPackageRepository;
    }

    /// <inheritdoc />
    public async Task<RuntimeArtifactCompatibilityValidationResult> ValidateAsync(
        JsonNode artifactPayload,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (artifactPayload is null)
        {
            return RuntimeArtifactCompatibilityValidationResult.Failure(
                "ArtifactPayloadMissing",
                "Artifact payload is required for runtime compatibility validation.");
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
            return RuntimeArtifactCompatibilityValidationResult.Failure(
                "ArtifactPayloadNotDeserializable",
                $"Artifact payload could not be deserialized as an orchestration artifact: {exception.Message}");
        }

        if (artifact is null)
        {
            return RuntimeArtifactCompatibilityValidationResult.Failure(
                "ArtifactPayloadNotDeserializable",
                "Artifact payload could not be deserialized as an orchestration artifact.");
        }

        var result = await ValidateArtifactAsync(_artifactMigrator.Migrate(artifact), cancellationToken);
        return result;
    }

    private async Task<RuntimeArtifactCompatibilityValidationResult> ValidateArtifactAsync(
        OrchestrationArtifact artifact,
        CancellationToken cancellationToken)
    {
        var stages = artifact.StageDefinitions?.OrderBy(stage => stage.Order).ToArray() ?? [];
        var bundleResult = await ValidateRequiredBundlesAsync(artifact, cancellationToken);
        if (!bundleResult.Succeeded)
        {
            return bundleResult;
        }

        var capabilityResult = await ValidateRequiredCapabilitiesAsync(artifact, cancellationToken);
        if (!capabilityResult.Succeeded)
        {
            return capabilityResult;
        }

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

            var compensationResult = ValidateTriggerCompensation(trigger);
            if (!compensationResult.Succeeded)
            {
                return compensationResult;
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

    private RuntimeArtifactCompatibilityValidationResult ValidateTask(TaskArtifact task, StageArtifact stage)
    {
        if (!_adapterRegistry.TryGet(task.Kind, out var adapter))
        {
            return Failure(
                "TaskRuntimeAdapterNotConfigured",
                $"Task '{task.Key}' in stage '{stage.Key}' uses '{task.Kind}', but no runtime task adapter is configured for that task kind.");
        }

        var adapterResult = adapter.ValidateTask(task, stage.Key);
        if (!adapterResult.Succeeded)
        {
            return adapterResult;
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

        var requestValidationResult = ValidateRequestValidation(task.Configuration, $"task '{task.Key}' request");
        if (!requestValidationResult.Succeeded)
        {
            return requestValidationResult;
        }

        var responseValidationResult = ValidateResponseValidation(task.Configuration, $"task '{task.Key}' response");
        if (!responseValidationResult.Succeeded)
        {
            return responseValidationResult;
        }

        return ValidateCompensation(task, stage);
    }

    private RuntimeArtifactCompatibilityValidationResult ValidateTriggerCompensation(TriggerBindingArtifact trigger)
    {
        if (trigger.Compensation is null || trigger.Compensation.Configuration is null)
        {
            return RuntimeArtifactCompatibilityValidationResult.Success();
        }

        if (!_adapterRegistry.TryGet(trigger.Compensation.CompensationTaskKind, out var adapter))
        {
            return Failure(
                "CompensationTaskRuntimeAdapterNotConfigured",
                $"Compensation for trigger '{trigger.Id}' uses '{trigger.Compensation.CompensationTaskKind}', but no runtime task adapter is configured for that compensation task kind.");
        }

        var syntheticTask = new TaskArtifact(
            trigger.Id,
            $"trigger:{trigger.Id}",
            trigger.Description ?? $"trigger:{trigger.Id}",
            0,
            string.Empty,
            trigger.Compensation.CompensationTaskKind,
            TaskExecutionMode.Sequential,
            null,
            null,
            trigger.Compensation.Transformation,
            trigger.Compensation.Configuration,
            trigger.Compensation.RetryPolicy,
            trigger.Compensation.TimeoutPolicy,
            OnErrorPolicy.Stop,
            trigger.Compensation,
            trigger.Compensation.DispatchType,
            true);
        var adapterResult = adapter.ValidateCompensation(syntheticTask, "trigger");
        if (!adapterResult.Succeeded)
        {
            return adapterResult;
        }

        var retryPolicyResult = ValidateRetryPolicy(trigger.Compensation.RetryPolicy, $"compensation for trigger '{trigger.Id}'");
        if (!retryPolicyResult.Succeeded)
        {
            return retryPolicyResult;
        }

        var timeoutPolicyResult = ValidateTimeoutPolicy(trigger.Compensation.TimeoutPolicy, $"compensation for trigger '{trigger.Id}'");
        if (!timeoutPolicyResult.Succeeded)
        {
            return timeoutPolicyResult;
        }

        var conditionResult = ValidateCondition(trigger.Compensation.ExecutionCondition, $"compensation for trigger '{trigger.Id}'");
        if (!conditionResult.Succeeded)
        {
            return conditionResult;
        }

        return ValidateTransformation(trigger.Compensation.Transformation, $"compensation for trigger '{trigger.Id}'");
    }

    private RuntimeArtifactCompatibilityValidationResult ValidateCompensation(TaskArtifact task, StageArtifact stage)
    {
        if (task.Compensation is null || task.Compensation.Configuration is null)
        {
            return RuntimeArtifactCompatibilityValidationResult.Success();
        }

        if (!_adapterRegistry.TryGet(task.Compensation.CompensationTaskKind, out var adapter))
        {
            return Failure(
                "CompensationTaskRuntimeAdapterNotConfigured",
                $"Compensation for task '{task.Key}' in stage '{stage.Key}' uses '{task.Compensation.CompensationTaskKind}', but no runtime task adapter is configured for that compensation task kind.");
        }

        var adapterResult = adapter.ValidateCompensation(task, stage.Key);
        if (!adapterResult.Succeeded)
        {
            return adapterResult;
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

        if (retryPolicy.MaxRetries > 0 &&
            retryPolicy.RetryableErrorCodes?.Any(code => !string.IsNullOrWhiteSpace(code)) != true)
        {
            return Failure(
                "RetryableErrorCodesMissing",
                $"The retry policy configured for {owner} must list explicit retryable error codes when max retries is greater than zero.");
        }

        return RuntimeArtifactCompatibilityValidationResult.Success();
    }

    private async Task<RuntimeArtifactCompatibilityValidationResult> ValidateRequiredBundlesAsync(
        OrchestrationArtifact artifact,
        CancellationToken cancellationToken)
    {
        foreach (var bundle in artifact.RequiredBundles ?? Array.Empty<RequiredExtensionBundleArtifact>())
        {
            if (string.IsNullOrWhiteSpace(bundle.ExtensionKey) ||
                string.Equals(bundle.ExtensionKey, ExtensionConstants.BuiltInExtensionKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(bundle.BundleId))
            {
                return Failure(
                    "ExtensionBundleReferenceInvalid",
                    $"Artifact contains an invalid bundle reference for extension '{bundle.ExtensionKey}'.");
            }

            if (_extensionPackageRepository is not null &&
                await _extensionPackageRepository.TryGetActiveBundleAsync(
                    bundle.BundleId,
                    bundle.ExtensionKey,
                    bundle.Version,
                    bundle.Sha256,
                    cancellationToken) is not null)
            {
                continue;
            }

            return Failure(
                "ExtensionBundleNotActivated",
                $"Artifact requires extension bundle '{bundle.BundleId}' for '{bundle.ExtensionKey}' version '{bundle.Version}', but that bundle is not activated on this runtime node.");
        }

        return RuntimeArtifactCompatibilityValidationResult.Success();
    }

    private async Task<RuntimeArtifactCompatibilityValidationResult> ValidateRequiredCapabilitiesAsync(
        OrchestrationArtifact artifact,
        CancellationToken cancellationToken)
    {
        foreach (var capability in artifact.RequiredCapabilities ?? Array.Empty<RequiredCapabilityArtifact>())
        {
            if (string.IsNullOrWhiteSpace(capability.ExtensionKey) ||
                string.Equals(capability.ExtensionKey, ExtensionConstants.BuiltInExtensionKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (_extensionPackageRepository is not null &&
                await _extensionPackageRepository.TryGetActiveAsync(
                    capability.ExtensionKey,
                    capability.Version,
                    cancellationToken) is RuntimeExtensionPackage package &&
                PackageContainsCapability(package, capability))
            {
                continue;
            }

            return Failure(
                "ExtensionCapabilityNotConfigured",
                $"Artifact requires extension capability '{capability.ExtensionKey}/{capability.CapabilityKey}', but external extension execution is not enabled on this runtime node.");
        }

        return RuntimeArtifactCompatibilityValidationResult.Success();
    }

    private static bool PackageContainsCapability(
        RuntimeExtensionPackage package,
        RequiredCapabilityArtifact capability)
        => package.Manifest?.Capabilities?.Any(descriptor =>
            string.Equals(descriptor.Key.Value, capability.CapabilityKey, StringComparison.OrdinalIgnoreCase) &&
            descriptor.Version.Equals(capability.Version) &&
            (string.IsNullOrWhiteSpace(capability.Kind) ||
                string.IsNullOrWhiteSpace(descriptor.Kind) ||
                string.Equals(descriptor.Kind, capability.Kind, StringComparison.OrdinalIgnoreCase))) == true;

    private static RuntimeArtifactCompatibilityValidationResult ValidateRequestValidation(
        ITaskConfigurationArtifact configuration,
        string owner)
        => configuration is MessagingTaskConfigurationArtifact messaging
            ? ValidateValidation(
                messaging.RequestValidation,
                messaging.RequestSchemaBinding ?? messaging.SchemaBinding,
                owner)
            : RuntimeArtifactCompatibilityValidationResult.Success();

    private static RuntimeArtifactCompatibilityValidationResult ValidateResponseValidation(
        ITaskConfigurationArtifact configuration,
        string owner)
        => configuration is MessagingTaskConfigurationArtifact messaging
            ? ValidateValidation(
                messaging.ResponseValidation,
                messaging.ResponseSchemaBinding,
                owner)
            : RuntimeArtifactCompatibilityValidationResult.Success();

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
