using Krackend.Sagas.Orchestrations.Runtime.Engine.Control;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Persistence;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Promotion;
using Krackend.Sagas.Orchestrations.Runtime.Operations;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine
{
    internal class SagaEngine : ISagaEngine, IBatchedSagaEngine
    {
        private const int MaxDecisionCycles = 100;
        private readonly IPromoter _promoter;
        private readonly IDecisionControl _decisionControl;
        private readonly IDecisionExecutor _decisionExecutor;
        private readonly IOrchestrationInstanceCoordinator _coordinator;
        private readonly IRuntimeStateTransaction _stateTransaction;
        private readonly IRuntimeAdmissionController _admissionController;
        private readonly ILogger<SagaEngine> _logger;

        public SagaEngine(
            IPromoter promoter,
            IDecisionControl decisionControl,
            IDecisionExecutor decisionExecutor,
            IOrchestrationInstanceCoordinator coordinator,
            IRuntimeStateTransaction stateTransaction,
            IRuntimeAdmissionController admissionController,
            ILogger<SagaEngine> logger)
        {
            _promoter = promoter ?? throw new ArgumentNullException(nameof(promoter));
            _decisionControl = decisionControl ?? throw new ArgumentNullException(nameof(decisionControl));
            _decisionExecutor = decisionExecutor ?? throw new ArgumentNullException(nameof(decisionExecutor));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _stateTransaction = stateTransaction ?? throw new ArgumentNullException(nameof(stateTransaction));
            _admissionController = admissionController ?? throw new ArgumentNullException(nameof(admissionController));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartOrchestrationAsync(StartIntent intent, CancellationToken cancellationToken = default)
        {
            await _admissionController.EnsureAcceptedAsync(RuntimeAdmissionOperation.TriggerIntake, cancellationToken);

            var proposedInstanceId = Id.NewDistributed();
            var promotionRequest = new PromotionRequest
            {
                ArtifactId = intent.ArtifactId,
                MessageMetadata = intent.MessageMetadata,
                StartIdempotencyKey = intent.StartIdempotencyKey,
                PropagationMetadata = intent.PropagationMetadata,
                Payload = intent.Payload,
                ProposedInstanceId = proposedInstanceId
            };

            PromotionResult promotionResult = null;
            Task StartWithinCoordinator(CancellationToken token)
                => ExecuteActivationBatchAsync(async innerToken =>
                {
                    promotionResult = await _promoter.PromoteToInstanceAsync(promotionRequest, innerToken);
                    if (!promotionResult.Success || promotionResult.AlreadyPromoted)
                        return;

                    await InternalOrchestrateAsync(
                        BuildForwardIntent(intent, promotionResult, promotionResult.Instance),
                        innerToken);
                }, token);

            var acquired = _coordinator is INewOrchestrationInstanceCoordinator newCoordinator
                ? await newCoordinator.TryExecuteNewAsync(
                    proposedInstanceId,
                    StartWithinCoordinator,
                    cancellationToken,
                    waitForLocalGate: intent.WaitForInstanceLease)
                : await _coordinator.TryExecuteAsync(
                    proposedInstanceId,
                    StartWithinCoordinator,
                    cancellationToken,
                    waitForLease: intent.WaitForInstanceLease);

            if (!acquired)
            {
                throw new OrchestrationInstanceLeaseUnavailableException(proposedInstanceId);
            }

            if (promotionResult is null || !promotionResult.Success)
            {
                _logger.LogWarning(
                    "Cannot promote a new SAGA instance with artifact id '{id}': {message}",
                    intent.ArtifactId,
                    promotionResult?.ErrorMessage ?? "The promotion activation returned no result.");
                return;
            }

            if (promotionResult.AlreadyPromoted)
            {
                await CoordinateOrchestrationAsync(
                    ParseInstanceId(promotionResult.InstanceId),
                    BuildForwardIntent(intent, promotionResult),
                    cancellationToken);
            }
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

        public async Task OrchestrateBatchAsync(
            IReadOnlyCollection<ForwardIntent> intents,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(intents);
            if (intents.Count == 0)
                return;

            await _admissionController.EnsureAcceptedAsync(
                RuntimeAdmissionOperation.OrchestrationExecution,
                cancellationToken);

            var batch = intents as IReadOnlyList<ForwardIntent> ?? intents.ToArray();
            var instanceId = TryResolveInstanceId(batch[0].MessageMetadata)
                ?? throw new ArgumentException(
                    "Every batched callback must identify its orchestration instance.",
                    nameof(intents));
            if (batch.Any(intent => TryResolveInstanceId(intent.MessageMetadata) != instanceId))
                throw new ArgumentException(
                    "A callback batch cannot contain more than one orchestration instance.",
                    nameof(intents));

            var acquired = await _coordinator.TryExecuteAsync(
                instanceId,
                token => ExecuteActivationBatchAsync(async innerToken =>
                {
                    // These callbacks share mutable saga state and therefore execute in
                    // deterministic arrival order inside the instance transaction. Other
                    // instance batches run independently and concurrently.
                    foreach (var intent in batch)
                        await InternalOrchestrateAsync(intent, innerToken);
                }, token),
                cancellationToken,
                waitForLease: batch.Any(intent => intent.WaitForInstanceLease));

            if (!acquired)
                throw new OrchestrationInstanceLeaseUnavailableException(instanceId);
        }

        private async Task CoordinateOrchestrationAsync(
            Id instanceId,
            ForwardIntent intent,
            CancellationToken cancellationToken)
        {
            if (_coordinator is IOrchestrationPostLeaseWork parent && parent.IsWithinMutation(instanceId))
            {
                // The parent mutation owns activation. Observe synchronous results
                // only after it releases the lease and activates its children.
                await _stateTransaction.ExecuteBatchAsync(
                    token => InternalOrchestrateAsync(intent, token), cancellationToken);
                if (parent.HasPendingWork(instanceId))
                {
                    var continuation = new ForwardIntent
                    {
                        WaitForInstanceLease = intent.WaitForInstanceLease,
                        ArtifactId = intent.ArtifactId, IngressTransport = intent.IngressTransport,
                        MessageMetadata = ClearCallbackSignal(intent.MessageMetadata),
                        PropagationMetadata = intent.PropagationMetadata, Payload = intent.Payload
                    };
                    await parent.ExecuteAfterLeaseReleaseAsync(instanceId,
                        token => CoordinateOrchestrationAsync(instanceId, continuation, token), cancellationToken);
                }
                return;
            }
            var acquired = await _coordinator.TryExecuteAsync(
                instanceId,
                token => ExecuteActivationBatchAsync(
                    innerToken => InternalOrchestrateAsync(intent, innerToken), token),
                cancellationToken,
                waitForLease: intent.WaitForInstanceLease);

            if (!acquired)
            {
                throw new OrchestrationInstanceLeaseUnavailableException(instanceId);
            }
        }

        private async Task ExecuteActivationBatchAsync(
            Func<CancellationToken, Task> activation,
            CancellationToken cancellationToken)
        {
            try
            {
                await _stateTransaction.ExecuteBatchAsync(activation, cancellationToken);
                if (_decisionControl is IOrchestrationDecisionStateLifecycle lifecycle)
                    lifecycle.CommitActivation();
            }
            catch
            {
                if (_decisionControl is IOrchestrationDecisionStateLifecycle lifecycle)
                    lifecycle.AbortActivation();
                throw;
            }
        }

        private async Task InternalOrchestrateAsync(ForwardIntent intent, CancellationToken cancellationToken = default)
        {
            var messageMetadata = intent.MessageMetadata;
            var propagationMetadata = intent.PropagationMetadata;
            var executionResultMetadata = intent.ExecutionResultMetadata;
            var instanceId = TryResolveInstanceId(messageMetadata);

            for (var cycle = 0; cycle < MaxDecisionCycles; cycle++)
            {
                var decisionRequest = new DecisionRequest
                {
                    ArtifactId = intent.ArtifactId,
                    MessageMetadata = messageMetadata,
                    PropagationMetadata = propagationMetadata,
                    ExecutionResultMetadata = executionResultMetadata,
                    Payload = intent.Payload,
                    PreparedInstance = intent.PreparedInstance
                };

                var decisionStarted = Stopwatch.GetTimestamp();
                IReadOnlyCollection<IDecision> decisions;
                try
                {
                    decisions = await _decisionControl.DecideAsync(decisionRequest, cancellationToken);
                }
                finally
                {
                    RuntimeConcurrencyMetrics.DecisionMilliseconds.Record(
                        Stopwatch.GetElapsedTime(decisionStarted).TotalMilliseconds);
                }
                if (decisions.Count == 0)
                {
                    return;
                }

                var executionStarted = Stopwatch.GetTimestamp();
                try
                {
                    foreach (var decision in decisions)
                    {
                        await _decisionExecutor.ExecuteAsync(decision, cancellationToken);
                    }
                }
                finally
                {
                    RuntimeConcurrencyMetrics.DecisionExecutionMilliseconds.Record(
                        Stopwatch.GetElapsedTime(executionStarted).TotalMilliseconds,
                        new KeyValuePair<string, object?>("count", decisions.Count),
                        new KeyValuePair<string, object?>("decision", string.Join(
                            "+",
                            decisions.Select(static decision => decision.GetType().Name))));
                }

                // A dispatch intent is now durable in the activation transaction.
                // Do not re-query state that cannot advance until the isolated
                // post-lease delivery has run and produced its callback.
                if (decisions.Any(static decision => decision is DispatchTaskDecision or RetryDecision) &&
                    instanceId.HasValue &&
                    _coordinator is IOrchestrationPostLeaseWork postLeaseWork &&
                    postLeaseWork.HasPendingWork(instanceId.Value))
                {
                    return;
                }

                if (decisions.Any(static decision => decision is CompleteCallbackDecision))
                {
                    messageMetadata = ClearCallbackSignal(messageMetadata);
                    executionResultMetadata = null;
                }
            }

            _logger.LogWarning("SAGA engine reached the maximum decision cycles for artifact id '{id}'.", intent.ArtifactId);
        }

        private static ForwardIntent BuildForwardIntent(
            StartIntent intent,
            PromotionResult promotionResult,
            Abstractions.Runtime.OrchestrationInstance preparedInstance = null)
        {
            var metadata = intent.MessageMetadata ?? new OrchestrationMessageMetadata();
            metadata.SagaId = promotionResult.SagaId;
            metadata.OrchestrationInstanceId = promotionResult.InstanceId;
            metadata.CorrelationId = string.IsNullOrWhiteSpace(metadata.CorrelationId)
                ? promotionResult.CorrelationId
                : metadata.CorrelationId;

            return new ForwardIntent
            {
                WaitForInstanceLease = intent.WaitForInstanceLease,
                ArtifactId = intent.ArtifactId,
                IngressTransport = intent.IngressTransport,
                MessageMetadata = metadata,
                PropagationMetadata = intent.PropagationMetadata,
                Payload = intent.Payload,
                PreparedInstance = preparedInstance
            };
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
