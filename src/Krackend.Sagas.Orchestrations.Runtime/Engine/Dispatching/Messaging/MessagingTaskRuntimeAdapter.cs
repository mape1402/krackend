namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Metadata;

/// <summary>
/// Runtime adapter for messaging task artifacts.
/// </summary>
public sealed class MessagingTaskRuntimeAdapter : ITaskRuntimeAdapter
{
    private readonly IMessagingCommandSerializer _messagingCommandSerializer;
    private readonly IGetIngressConfigurationByArtifactAccessor _ingressConfigurationAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingTaskRuntimeAdapter"/> class for validation-only usage.
    /// </summary>
    public MessagingTaskRuntimeAdapter()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingTaskRuntimeAdapter"/> class.
    /// </summary>
    /// <param name="messagingCommandSerializer">Messaging command serializer.</param>
    /// <param name="ingressConfigurationAccessor">Ingress configuration accessor.</param>
    public MessagingTaskRuntimeAdapter(
        IMessagingCommandSerializer messagingCommandSerializer,
        IGetIngressConfigurationByArtifactAccessor ingressConfigurationAccessor)
    {
        _messagingCommandSerializer = messagingCommandSerializer ?? throw new ArgumentNullException(nameof(messagingCommandSerializer));
        _ingressConfigurationAccessor = ingressConfigurationAccessor ?? throw new ArgumentNullException(nameof(ingressConfigurationAccessor));
    }

    /// <inheritdoc />
    public TaskKind TaskKind => TaskKind.Messaging;

    /// <inheritdoc />
    public RuntimeArtifactCompatibilityValidationResult ValidateTask(TaskArtifact task, string stageKey)
    {
        if (task.Configuration is not MessagingTaskConfigurationArtifact messaging)
        {
            return Failure(
                "TaskConfigurationNotSupported",
                $"Task '{task.Key}' in stage '{stageKey}' does not contain messaging task configuration.");
        }

        if (string.IsNullOrWhiteSpace(messaging.Topic))
        {
            return Failure(
                "MessagingTopicMissing",
                $"Task '{task.Key}' in stage '{stageKey}' does not contain a messaging topic.");
        }

        if (task.DispatchType is not (TaskDispatchType.FireAndForget or TaskDispatchType.FireAndWaitCallback))
        {
            return Failure(
                "MessagingDispatchTypeNotSupported",
                $"Task '{task.Key}' in stage '{stageKey}' uses '{task.DispatchType}', but messaging tasks must use FireAndForget or FireAndWaitCallback.");
        }

        return RuntimeArtifactCompatibilityValidationResult.Success();
    }

    /// <inheritdoc />
    public RuntimeArtifactCompatibilityValidationResult ValidateCompensation(TaskArtifact task, string stageKey)
    {
        if (task.Compensation is null || task.Compensation.Configuration is null)
        {
            return RuntimeArtifactCompatibilityValidationResult.Success();
        }

        if (task.Compensation.CompensationTaskKind != TaskKind.Messaging)
        {
            return Failure(
                "CompensationTaskKindNotSupported",
                $"Compensation for task '{task.Key}' in stage '{stageKey}' uses '{task.Compensation.CompensationTaskKind}', but no runtime adapter is configured for that compensation task kind.");
        }

        if (task.Compensation.Configuration is not MessagingTaskConfigurationArtifact messaging)
        {
            return Failure(
                "CompensationConfigurationNotSupported",
                $"Compensation for task '{task.Key}' in stage '{stageKey}' does not contain messaging task configuration.");
        }

        if (string.IsNullOrWhiteSpace(messaging.Topic))
        {
            return Failure(
                "CompensationMessagingTopicMissing",
                $"Compensation for task '{task.Key}' in stage '{stageKey}' does not contain a messaging topic.");
        }

        if (task.Compensation.DispatchType != TaskDispatchType.FireAndForget)
        {
            return Failure(
                "CompensationDispatchTypeNotSupported",
                $"Compensation for task '{task.Key}' in stage '{stageKey}' uses '{task.Compensation.DispatchType}', but messaging compensations currently run as FireAndForget.");
        }

        return RuntimeArtifactCompatibilityValidationResult.Success();
    }

    /// <inheritdoc />
    public string GetDestination(TaskArtifact task)
    {
        var messaging = GetMessagingConfiguration(task);
        return $"{messaging.Topic}:{messaging.Version}";
    }

    /// <inheritdoc />
    public string GetCompensationDestination(CompensationArtifact compensation)
    {
        var messaging = GetMessagingConfiguration(compensation);
        return $"{messaging.Topic}:{messaging.Version}";
    }

    /// <inheritdoc />
    public async Task<TaskRuntimeCommandDescriptor> BuildCommandAsync(
        TaskRuntimeCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureRuntimeDependencies();

        var messagingConfiguration = GetMessagingConfiguration(request.Task);
        var replyAddress = await ResolveBackchannelReplyAddressAsync(request.Instance, cancellationToken);
        if (request.Task.DispatchType != TaskDispatchType.FireAndForget && replyAddress is null)
        {
            throw new InvalidOperationException(
                $"Task '{request.Task.Key}' requires a backchannel reply address, but none was projected for artifact '{request.Instance.RuntimeOrchestrationArtifactId}'.");
        }

        return new TaskRuntimeCommandDescriptor
        {
            Transport = RemoteCommandTransport.Messaging,
            SettingsPayload = _messagingCommandSerializer.Serialize(new MessagingCommand
            {
                Topic = messagingConfiguration.Topic,
                Version = messagingConfiguration.Version.ToString(),
                Payload = request.Payload
            }),
            ReplyAddress = replyAddress
        };
    }

    /// <inheritdoc />
    public async Task<TaskRuntimeCommandDescriptor> BuildCompensationCommandAsync(
        TaskRuntimeCompensationCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureRuntimeDependencies();

        var messagingConfiguration = GetMessagingConfiguration(request.Compensation);
        var replyAddress = await ResolveBackchannelReplyAddressAsync(request.Instance, cancellationToken);
        return new TaskRuntimeCommandDescriptor
        {
            Transport = RemoteCommandTransport.Messaging,
            SettingsPayload = _messagingCommandSerializer.Serialize(new MessagingCommand
            {
                Topic = messagingConfiguration.Topic,
                Version = messagingConfiguration.Version.ToString(),
                Payload = request.Payload
            }),
            ReplyAddress = replyAddress
        };
    }

    private static MessagingTaskConfigurationArtifact GetMessagingConfiguration(TaskArtifact task)
        => task.Configuration as MessagingTaskConfigurationArtifact
            ?? throw new InvalidOperationException($"Task '{task.Key}' does not contain a messaging configuration.");

    private static MessagingTaskConfigurationArtifact GetMessagingConfiguration(CompensationArtifact compensation)
        => compensation.Configuration as MessagingTaskConfigurationArtifact
            ?? throw new InvalidOperationException($"Compensation task kind '{compensation.CompensationTaskKind}' does not contain a messaging configuration.");

    private async Task<OrchestrationReplyAddress> ResolveBackchannelReplyAddressAsync(
        OrchestrationInstance instance,
        CancellationToken cancellationToken)
    {
        var configurations = await _ingressConfigurationAccessor.GetConfigurationAsync(
            instance.RuntimeOrchestrationArtifactId.ToString(),
            cancellationToken);
        var backchannel = configurations.FirstOrDefault(x =>
            x.IngressKind == IngressKind.Backchannel &&
            x.IngressTransport == IngressTransport.Messaging);

        return backchannel is null
            ? null
            : new OrchestrationReplyAddress
            {
                Transport = OrchestrationTransportNames.Messaging,
                SettingsPayload = backchannel.SettingsPayload
            };
    }

    private void EnsureRuntimeDependencies()
    {
        if (_messagingCommandSerializer is null || _ingressConfigurationAccessor is null)
        {
            throw new InvalidOperationException("Messaging runtime adapter was created without runtime dispatch dependencies.");
        }
    }

    private static RuntimeArtifactCompatibilityValidationResult Failure(string errorCode, string errorMessage)
        => RuntimeArtifactCompatibilityValidationResult.Failure(errorCode, errorMessage);
}
