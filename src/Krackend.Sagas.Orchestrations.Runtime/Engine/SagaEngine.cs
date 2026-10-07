using Krackend.Sagas.Orchestrations.Runtime.Engine.Control;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Promotion;
using Krackend.Sagas.Orchestrations.Runtime.Operations;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Microsoft.Extensions.Logging;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine
{
    internal class SagaEngine : ISagaEngine
    {
        private const int MaxDecisionCycles = 100;
        private readonly IPromoter _promoter;
        private readonly IDecisionControl _decisionControl;
        private readonly IDecisionExecutor _decisionExecutor;
        private readonly IOrchestrationInstanceCoordinator _coordinator;
        private readonly IRuntimeAdmissionController _admissionController;
        private readonly ILogger<SagaEngine> _logger;

        public SagaEngine(
            IPromoter promoter,
            IDecisionControl decisionControl,
            IDecisionExecutor decisionExecutor,
            IOrchestrationInstanceCoordinator coordinator,
            IRuntimeAdmissionController admissionController,
            ILogger<SagaEngine> logger)
        {
            _promoter = promoter ?? throw new ArgumentNullException(nameof(promoter));
            _decisionControl = decisionControl ?? throw new ArgumentNullException(nameof(decisionControl));
            _decisionExecutor = decisionExecutor ?? throw new ArgumentNullException(nameof(decisionExecutor));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _admissionController = admissionController ?? throw new ArgumentNullException(nameof(admissionController));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartOrchestrationAsync(StartIntent intent, CancellationToken cancellationToken = default)
        {
            await _admissionController.EnsureAcceptedAsync(RuntimeAdmissionOperation.TriggerIntake, cancellationToken);

            var promotionRequest = new PromotionRequest
            {
                ArtifactId = intent.ArtifactId,
                MessageMetadata = intent.MessageMetadata,
                StartIdempotencyKey = intent.StartIdempotencyKey,
                PropagationMetadata = intent.PropagationMetadata,
                Payload = intent.Payload
            };
            var promotionResult = await _promoter.PromoteToInstanceAsync(promotionRequest, cancellationToken);

            if (!promotionResult.Success)
            {
                _logger.LogWarning("Cannot promote a new SAGA instance with artifact id '{id}': {message}", intent.ArtifactId, promotionResult.ErrorMessage);
                return;
            }

            var metadata = intent.MessageMetadata ?? new OrchestrationMessageMetadata();
            metadata.SagaId = promotionResult.SagaId;
            metadata.OrchestrationInstanceId = promotionResult.InstanceId;
            metadata.CorrelationId = string.IsNullOrWhiteSpace(metadata.CorrelationId)
                ? promotionResult.CorrelationId
                : metadata.CorrelationId;

            var forwardIntent = new ForwardIntent
            {
                ArtifactId = intent.ArtifactId,
                IngressTransport = intent.IngressTransport,
                MessageMetadata = metadata,
                PropagationMetadata = intent.PropagationMetadata,
                Payload = intent.Payload
            };

            await CoordinateOrchestrationAsync(
                ParseInstanceId(promotionResult.InstanceId),
                forwardIntent,
                cancellationToken);
        }

        public async Task OrchestrateAsync(ForwardIntent intent, CancellationToken cancellationToken = default)
        {
            await _admissionController.EnsureAcceptedAsync(RuntimeAdmissionOperation.OrchestrationExecution, cancellationToken);

            var instanceId = TryResolveInstanceId(intent.MessageMetadata);
            if (instanceId.HasValue)
            {
                await CoordinateOrchestrationAsync(instanceId.Value, intent, cancellationToken);
                return;
            }

            await InternalOrchestrateAsync(intent, cancellationToken);
        }

        private async Task CoordinateOrchestrationAsync(
            Id instanceId,
            ForwardIntent intent,
            CancellationToken cancellationToken)
        {
            var acquired = await _coordinator.TryExecuteAsync(
                instanceId,
                token => InternalOrchestrateAsync(intent, token),
                cancellationToken);

            if (!acquired)
            {
                throw new OrchestrationInstanceLeaseUnavailableException(instanceId);
            }
        }

        private async Task InternalOrchestrateAsync(ForwardIntent intent, CancellationToken cancellationToken = default)
        {
            var messageMetadata = intent.MessageMetadata;
            var propagationMetadata = intent.PropagationMetadata;
            var executionResultMetadata = intent.ExecutionResultMetadata;

            for (var cycle = 0; cycle < MaxDecisionCycles; cycle++)
            {
                var decisionRequest = new DecisionRequest
                {
                    ArtifactId = intent.ArtifactId,
                    MessageMetadata = messageMetadata,
                    PropagationMetadata = propagationMetadata,
                    ExecutionResultMetadata = executionResultMetadata,
                    Payload = intent.Payload
                };

                var decisions = await _decisionControl.DecideAsync(decisionRequest, cancellationToken);
                if (decisions.Count == 0)
                {
                    return;
                }

                foreach (var decision in decisions)
                {
                    await _decisionExecutor.ExecuteAsync(decision, cancellationToken);
                }

                if (decisions.Any(static decision => decision is CompleteCallbackDecision))
                {
                    messageMetadata = ClearCallbackSignal(messageMetadata);
                    executionResultMetadata = null;
                }
            }

            _logger.LogWarning("SAGA engine reached the maximum decision cycles for artifact id '{id}'.", intent.ArtifactId);
        }

        private static OrchestrationMessageMetadata ClearCallbackSignal(OrchestrationMessageMetadata metadata)
            => metadata is null
                ? null
                : new OrchestrationMessageMetadata
                {
                    SagaId = metadata.SagaId,
                    OrchestrationInstanceId = metadata.OrchestrationInstanceId,
                    CurrentStage = metadata.CurrentStage,
                    CurrentTasks = metadata.CurrentTasks,
                    CorrelationId = metadata.CorrelationId,
                    ReplyAddress = metadata.ReplyAddress
                };

        private static Id ParseInstanceId(string value)
        {
            if (!Ulid.TryParse(value, out var parsed))
            {
                throw new InvalidOperationException($"Orchestration instance id '{value}' is invalid.");
            }

            return new Id(parsed);
        }

        private static Id? TryResolveInstanceId(OrchestrationMessageMetadata metadata)
        {
            if (string.IsNullOrWhiteSpace(metadata?.OrchestrationInstanceId) ||
                !Ulid.TryParse(metadata.OrchestrationInstanceId, out var parsed))
            {
                return null;
            }

            return new Id(parsed);
        }
    }
}
