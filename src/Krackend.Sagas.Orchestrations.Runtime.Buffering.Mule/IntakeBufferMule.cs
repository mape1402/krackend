using Krackend.Sagas.Orchestrations.Runtime.Operations;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Microsoft.Extensions.Logging;
using Mule;
using Mule.Dispatching;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using System.Text.Json;
using System.Diagnostics;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Microsoft.Extensions.Hosting;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    internal class IntakeBufferMule : IIntakeBuffer
    {
        private readonly IMuleClient _muleClient;
        private readonly IRuntimeAdmissionController _admissionController;
        private readonly ILogger<IntakeBufferMule> _logger;
        private readonly IOrchestrationInstanceCoordinator _coordinator;
        private readonly ITaskDispatchRepository? _dispatches;
        private readonly IMuleBackchannelImmediateDispatcher? _immediateBackchannels;
        private readonly ISagaEngine? _sagaEngine;
        private readonly RuntimeForegroundActivity? _foregroundActivity;
        private readonly CancellationToken _applicationStopping;

        public IntakeBufferMule(
            IMuleClient muleClient,
            IRuntimeAdmissionController admissionController,
            ILogger<IntakeBufferMule> logger,
            IOrchestrationInstanceCoordinator coordinator,
            ITaskDispatchRepository? dispatches = null,
            IMuleBackchannelImmediateDispatcher? immediateBackchannels = null,
            ISagaEngine? sagaEngine = null,
            RuntimeForegroundActivity? foregroundActivity = null,
            IHostApplicationLifetime? applicationLifetime = null)
        {
            _muleClient = muleClient ?? throw new ArgumentNullException(nameof(muleClient));
            _admissionController = admissionController ?? throw new ArgumentNullException(nameof(admissionController));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _dispatches = dispatches;
            _immediateBackchannels = immediateBackchannels;
            _sagaEngine = sagaEngine;
            _foregroundActivity = foregroundActivity;
            _applicationStopping = applicationLifetime?.ApplicationStopping ?? CancellationToken.None;
        }

        public async Task EnqueueWorkAsync(WorkItem workItem, CancellationToken cancellationToken = default)
        {
            using var activity = _foregroundActivity?.Enter();
            var started = Stopwatch.GetTimestamp();
            try { await EnqueueWorkCoreAsync(workItem, cancellationToken); }
            finally
            {
                RuntimeConcurrencyMetrics.IntakeMilliseconds.Record(
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    new KeyValuePair<string, object?>("kind", workItem?.IngressKind.ToString() ?? "Unknown"));
            }
        }

        private async Task EnqueueWorkCoreAsync(WorkItem workItem, CancellationToken cancellationToken)
        {
            if (workItem is null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            await _admissionController.EnsureAcceptedAsync(
                workItem.IngressKind == Ingress.IngressKind.Backchannel
                    ? RuntimeAdmissionOperation.BackchannelIntake
                    : RuntimeAdmissionOperation.TriggerIntake,
                cancellationToken);

            // Once admission succeeds, the Runtime owns the mutation. A caller can
            // disconnect or time out while the accepted transition is committing;
            // propagating that request token can leave a stage advanced without its
            // next task or durable recovery action. Only host shutdown may cancel
            // accepted work from this point forward.
            var processingCancellation = _applicationStopping;

            switch (workItem.IngressKind)
            {
                case Ingress.IngressKind.Trigger:
                    var triggerDeduplicationKey = BuildTriggerDeduplicationKey(workItem);
                    if (_sagaEngine is not null && !string.IsNullOrWhiteSpace(triggerDeduplicationKey))
                    {
                        try
                        {
                            await _sagaEngine.StartOrchestrationAsync(new StartIntent
                            {
                                WaitForInstanceLease = false,
                                ArtifactId = workItem.ArtifactId,
                                MessageMetadata = workItem.MessageMetadata,
                                StartIdempotencyKey = TriggerAction.BuildStartIdempotencyKeyFromDeduplicationKey(triggerDeduplicationKey),
                                PropagationMetadata = workItem.PropagationMetadata,
                                IngressTransport = workItem.IngressTransport,
                                Payload = workItem.Payload
                            }, processingCancellation);
                            break;
                        }
                        catch (OperationCanceledException) when (processingCancellation.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            // Promotion is idempotent by the exact key Mule will use below.
                            // Materialize a durable action only when direct execution failed.
                            _logger.LogDebug(exception,
                                "Direct trigger execution failed for '{CorrelationId}'; activating durable Mule recovery.",
                                workItem.MessageMetadata?.CorrelationId);
                        }
                    }
                    await EnqueueDurableAsync(
                        MuleActionKeys.TriggerActionKey,
                        workItem,
                        options =>
                        {
                            options.Lane = MuleActionKeys.TriggerLane;
                            options.CorrelationId = workItem.MessageMetadata?.CorrelationId
                                ?? workItem.MessageMetadata?.SagaId;
                            options.DeduplicationKey = BuildTriggerDeduplicationKey(workItem);
                        },
                        processingCancellation);
                    break;

                case Ingress.IngressKind.Backchannel:
                    if (_dispatches is ITaskDispatchCallbackReceiptRepository receipts &&
                        System.Ulid.TryParse(workItem.MessageMetadata?.DispatchId, out var dispatchValue))
                    {
                        if (_sagaEngine is not null)
                        {
                            try
                            {
                                // Rabbit retains the unacknowledged delivery while this
                                // executes. Enter the engine directly when the instance is
                                // free; only an actual same-instance collision needs the
                                // mailbox below. Unrelated instances never share a queue.
                                await BackchannelAction.OrchestrateWhenDispatchIsReadyAsync(
                                    _sagaEngine,
                                    null,
                                    workItem,
                                    BackchannelAction.CreateIntent(workItem, waitForInstanceLease: false),
                                    processingCancellation,
                                    TimeSpan.Zero);
                                break;
                            }
                            catch (OperationCanceledException) when (processingCancellation.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception exception)
                            {
                                _logger.LogDebug(exception,
                                    "Direct callback execution failed for dispatch '{DispatchId}'; persisting recovery receipt.",
                                    workItem.MessageMetadata?.DispatchId);

                                if (_dispatches is IEmbeddedTaskTransportStateRepository
                                    {
                                        EmbedsCurrentTransportState: true
                                    })
                                {
                                    // Compact transport state has no TaskDispatch row on which
                                    // TryAcceptCallback can atomically stamp a receipt. Never treat
                                    // that missing row as a duplicate. The delivery that lost
                                    // the instance race waits in that instance's immediate mailbox
                                    // while Rabbit keeps it unsettled. Mule is materialized only if
                                    // this direct handoff itself fails.
                                    if (_immediateBackchannels is not null)
                                    {
                                        try
                                        {
                                            await _immediateBackchannels.DispatchDirectAsync(
                                                workItem,
                                                processingCancellation);
                                            break;
                                        }
                                        catch (OperationCanceledException) when (processingCancellation.IsCancellationRequested)
                                        {
                                            throw;
                                        }
                                        catch (Exception handoffException)
                                        {
                                            _logger.LogDebug(handoffException,
                                                "Immediate callback collision handoff failed for dispatch '{DispatchId}'; activating durable recovery.",
                                                workItem.MessageMetadata?.DispatchId);
                                        }
                                    }

                                    await EnqueueResponseAsync(workItem, processingCancellation);
                                    break;
                                }
                            }
                        }

                        workItem.CallbackReceiptId ??= Id.NewDistributed().ToString();
                        var receipt = await receipts.TryAcceptCallback(new Id(dispatchValue), workItem.CallbackReceiptId,
                            JsonSerializer.Serialize(workItem), DateTime.UtcNow, processingCancellation,
                            new TaskCallbackIdentity(ParseOptionalId(workItem.MessageMetadata.OrchestrationInstanceId),
                                ParseOptionalId(workItem.MessageMetadata.TaskExecutionId), workItem.MessageMetadata.Attempt,
                                ParseOptionalId(workItem.ArtifactId)));
                        if (receipt is not null)
                        {
                            if (!receipt.CallbackPending) break;
                            workItem = JsonSerializer.Deserialize<WorkItem>(receipt.PendingCallbackPayload)
                                ?? throw new InvalidOperationException("Accepted callback payload is missing.");
                            // Intake owns durable acceptance only. Hand the accepted receipt
                            // to the non-blocking direct dispatcher and return the transport
                            // consumer immediately. If the in-memory handoff is unavailable,
                            // the same callback falls back to a durable Mule action.
                            if (_immediateBackchannels?.TryDispatch(workItem) != true)
                                await EnqueueResponseAsync(workItem, processingCancellation);
                            break;
                        }
                        if (await _dispatches.TryGetById(new Id(dispatchValue), processingCancellation) is not null)
                        {
                            // A different artifact/attempt can share a physical consumer.
                            // It must not reserve the valid callback's deduplication key.
                            break;
                        }
                    }
                    async Task AcceptResponseAsync(CancellationToken token) => await EnqueueDurableAsync(
                        MuleActionKeys.BackchannelActionKey, workItem, options =>
                        {
                            options.Lane = MuleActionKeys.BackchannelLane;
                            options.CorrelationId = workItem.MessageMetadata?.OrchestrationInstanceId
                                ?? workItem.MessageMetadata?.CorrelationId;
                            options.DeduplicationKey = BuildBackchannelDeduplicationKey(workItem);
                        }, token);
                    if (System.Ulid.TryParse(workItem.MessageMetadata?.OrchestrationInstanceId, out var parsed))
                    {
                        var instanceId = new Id(parsed);
                        // Serialize only with another mutation of this exact instance.
                        // Waiting here avoids converting normal same-saga overlap into
                        // redelivery/recovery churn; unrelated instances have separate gates.
                        var accepted = await _coordinator.TryExecuteAsync(instanceId, AcceptResponseAsync, processingCancellation, waitForLease: true);
                        if (!accepted) throw new OrchestrationInstanceLeaseUnavailableException(instanceId);
                    }
                    else await AcceptResponseAsync(processingCancellation);
                    break;

                default:
                    _logger.LogWarning(
                        "Runtime work item for artifact '{artifactId}' was ignored because ingress kind '{ingressKind}' is not supported.",
                        workItem.ArtifactId,
                        workItem.IngressKind);
                    break;
            }
        }

        private Task EnqueueResponseAsync(WorkItem workItem, CancellationToken cancellationToken)
            => EnqueueDurableAsync(MuleActionKeys.BackchannelActionKey, workItem, options =>
            {
                options.Lane = MuleActionKeys.BackchannelLane;
                options.CorrelationId = workItem.MessageMetadata?.OrchestrationInstanceId ?? workItem.MessageMetadata?.CorrelationId;
                options.DeduplicationKey = BuildBackchannelDeduplicationKey(workItem);
            }, cancellationToken);

        private static Id? ParseOptionalId(string value)
            => string.IsNullOrWhiteSpace(value) ? null : new Id(System.Ulid.Parse(value));

        private async Task EnqueueDurableAsync(
            ActionKey actionKey,
            WorkItem workItem,
            Action<EnqueueOptions> configureOptions,
            CancellationToken cancellationToken)
        {
            try
            {
                await _muleClient.EnqueueAsync(
                    actionKey,
                    workItem,
                    options =>
                    {
                        configureOptions(options);
                    },
                    cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Mule durable action '{ActionKey}' could not be enqueued. The ingress transport will be allowed to redeliver the message.",
                    actionKey);
                throw;
            }
        }

        private string? BuildTriggerDeduplicationKey(WorkItem workItem)
        {
            var correlationId = workItem.MessageMetadata?.CorrelationId
                ?? workItem.MessageMetadata?.SagaId;

            return string.IsNullOrWhiteSpace(correlationId)
                ? null
                : $"trigger:{workItem.ArtifactId}:{correlationId}";
        }

        internal static string? BuildBackchannelDeduplicationKey(WorkItem workItem)
        {
            var metadata = workItem.MessageMetadata;
            if (metadata is null)
            {
                return null;
            }

            var dispatchId = metadata.DispatchId;
            if (!string.IsNullOrWhiteSpace(dispatchId))
            {
                return $"backchannel:{dispatchId}:{metadata.Attempt}";
            }

            var taskExecutionId = metadata.TaskExecutionId;
            if (!string.IsNullOrWhiteSpace(taskExecutionId))
            {
                return $"backchannel:{taskExecutionId}:{metadata.Attempt}";
            }

            return null;
        }
    }
}
