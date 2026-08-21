using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers
{
    internal sealed class CompensateInstanceDecisionHandler : IDecisionHandler<CompensateInstanceDecision>
    {
        private readonly IRuntimeArtifactResolver _artifactResolver;
        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly ITaskExecutionRepository _taskRepository;
        private readonly ICompensationExecutionRepository _compensationRepository;
        private readonly IExecutionTransitionRepository _transitionRepository;
        private readonly IRemoteCommandDispatcher _dispatcher;
        private readonly IMessagingCommandSerializer _messagingCommandSerializer;
        private readonly IGetIngressConfigurationByArtifactAccessor _ingressConfigurationAccessor;

        public CompensateInstanceDecisionHandler(
            IRuntimeArtifactResolver artifactResolver,
            IOrchestrationInstanceRepository instanceRepository,
            ITaskExecutionRepository taskRepository,
            ICompensationExecutionRepository compensationRepository,
            IExecutionTransitionRepository transitionRepository,
            IRemoteCommandDispatcher dispatcher,
            IMessagingCommandSerializer messagingCommandSerializer,
            IGetIngressConfigurationByArtifactAccessor ingressConfigurationAccessor)
        {
            _artifactResolver = artifactResolver ?? throw new ArgumentNullException(nameof(artifactResolver));
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
            _compensationRepository = compensationRepository ?? throw new ArgumentNullException(nameof(compensationRepository));
            _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _messagingCommandSerializer = messagingCommandSerializer ?? throw new ArgumentNullException(nameof(messagingCommandSerializer));
            _ingressConfigurationAccessor = ingressConfigurationAccessor ?? throw new ArgumentNullException(nameof(ingressConfigurationAccessor));
        }

        public async Task HandleAsync(CompensateInstanceDecision decision, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var instance = await _instanceRepository.GetById(decision.InstanceId, cancellationToken);
            var replyAddress = await ResolveBackchannelReplyAddressAsync(instance, cancellationToken);
            var completedTasks = (await _taskRepository.GetByInstanceId(decision.InstanceId, cancellationToken))
                .Where(x => x.Status == TaskExecutionStatus.Completed)
                .OrderByDescending(x => x.CompletedOnUtc)
                .ToArray();
            var resolvedArtifact = await _artifactResolver.ResolveAsync(decision.ArtifactId, cancellationToken);
            var taskArtifacts = resolvedArtifact.Artifact.StageDefinitions
                .SelectMany(x => x.TaskDefinitions)
                .ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);

            instance.Status = OrchestrationInstanceStatus.Compensating;
            instance.CompensationStartedOnUtc = now;
            instance.LastUpdatedOnUtc = now;
            await _instanceRepository.Update(instance, cancellationToken);

            foreach (var task in completedTasks)
            {
                if (!taskArtifacts.TryGetValue(task.TaskKey, out var taskArtifact))
                {
                    continue;
                }

                if (taskArtifact.Compensation?.Configuration is not MessagingTaskConfigurationArtifact messagingConfiguration)
                {
                    continue;
                }

                var compensation = new CompensationExecution
                {
                    Id = Id.New(),
                    OrchestrationInstanceId = instance.Id,
                    SourceTaskExecutionId = task.Id,
                    CompensationTaskKey = task.TaskKey,
                    Status = "Running",
                    StartedOnUtc = DateTime.UtcNow,
                    RequestPayload = string.IsNullOrWhiteSpace(decision.Payload) ? null : System.Text.Json.Nodes.JsonNode.Parse(decision.Payload)
                };
                await _compensationRepository.Create(compensation, cancellationToken);

                var command = new MessagingCommand
                {
                    Topic = messagingConfiguration.Topic,
                    Version = messagingConfiguration.Version.ToString(),
                    Payload = decision.Payload
                };
                await _dispatcher.DispatchAsync(new RemoteCommand
                {
                    Payload = decision.Payload,
                    RemoteCommandTransport = RemoteCommandTransport.Messaging,
                    SettingsPayload = _messagingCommandSerializer.Serialize(command),
                    OrchestrationInstanceId = instance.Id.ToString(),
                    TaskExecutionId = task.Id.ToString(),
                    TaskKey = task.TaskKey,
                    AwaitResponse = false,
                    MessageMetadata = new OrchestrationMessageMetadata
                    {
                        SagaId = instance.CorrelationId,
                        OrchestrationInstanceId = instance.Id.ToString(),
                        CorrelationId = instance.CorrelationId,
                        TaskExecutionId = task.Id.ToString(),
                        CurrentTasks = [task.TaskKey],
                        ReplyAddress = replyAddress
                    }
                }, cancellationToken);

                compensation.Status = "Completed";
                compensation.CompletedOnUtc = DateTime.UtcNow;
                await _compensationRepository.Update(compensation, cancellationToken);
            }

            instance.Status = OrchestrationInstanceStatus.Compensated;
            instance.CompensatedOnUtc = DateTime.UtcNow;
            instance.LastUpdatedOnUtc = instance.CompensatedOnUtc.Value;
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                TransitionType = "InstanceCompensated",
                FromStatus = OrchestrationInstanceStatus.Compensating.ToString(),
                ToStatus = OrchestrationInstanceStatus.Compensated.ToString(),
                OccurredOnUtc = instance.CompensatedOnUtc.Value,
                Message = "Compensation executed in reverse order for completed tasks.",
                ProducedBy = nameof(CompensateInstanceDecisionHandler)
            }, cancellationToken);
        }

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
    }
}
