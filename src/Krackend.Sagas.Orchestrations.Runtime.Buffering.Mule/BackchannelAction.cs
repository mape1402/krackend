using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Mule;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    /// <summary>
    /// Processes durable backchannel work items and forwards them to the saga engine.
    /// </summary>
    [MuleAction(MuleActionKeys.BackchannelAction)]
    public class BackchannelAction : IMuleAction<WorkItem>
    {
        private readonly ISagaEngine _sagaEngine;
        private readonly ITaskDispatchRepository? _dispatches;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackchannelAction"/> class.
        /// </summary>
        /// <param name="sagaEngine">Saga engine that advances orchestration instances.</param>
        public BackchannelAction(ISagaEngine sagaEngine, ITaskDispatchRepository? dispatches = null)
        {
            _sagaEngine = sagaEngine ?? throw new ArgumentNullException(nameof(sagaEngine));
            _dispatches = dispatches;
        }

        /// <inheritdoc />
        public async ValueTask ExecuteAsync(MuleActionContext<WorkItem> context, CancellationToken cancellationToken)
        {
            var workItem = context.Payload;

            var intent = CreateIntent(workItem);

            await MuleCooperativeExecution.RunAsync(context,
                () => OrchestrateWhenDispatchIsReadyAsync(_sagaEngine, _dispatches, workItem, intent, cancellationToken));
        }

        internal static ForwardIntent CreateIntent(WorkItem workItem, bool waitForInstanceLease = true) => new()
        {
            // A callback for the same saga must observe the mutation immediately
            // before it. Wait only on that instance's local gate instead of failing
            // fast and turning a normal overlap into repeated Mule/recovery work.
            // Other saga ids use different gates and continue fully in parallel.
            WaitForInstanceLease = waitForInstanceLease,
            ArtifactId = workItem.ArtifactId,
            IngressTransport = workItem.IngressTransport,
            MessageMetadata = workItem.MessageMetadata,
            PropagationMetadata = workItem.PropagationMetadata,
            ExecutionResultMetadata = workItem.ExecutionResultMetadata,
            Payload = workItem.Payload
        };

        internal static async Task OrchestrateWhenDispatchIsReadyAsync(
            ISagaEngine sagaEngine,
            ITaskDispatchRepository? dispatches,
            WorkItem workItem,
            ForwardIntent intent,
            CancellationToken cancellationToken,
            TimeSpan? directHandoffWindow = null)
        {
            var handoffWindow = directHandoffWindow ?? TimeSpan.FromSeconds(2);
            // Publication confirmation and sibling callbacks for the same saga release
            // their lease quickly. Enter the engine immediately: its joined state read
            // already validates the dispatch, task, attempt and instance. The previous
            // preflight polling query duplicated that read for every healthy callback.
            // Retry only an actual readiness race; after this bounded window Mule retains
            // the action for recovery.
            var handoffDeadline = DateTime.UtcNow.Add(handoffWindow);
            while (true)
            {
                try
                {
                    await sagaEngine.OrchestrateAsync(intent, cancellationToken);
                    return;
                }
                catch (OrchestrationInstanceLeaseUnavailableException) when (DateTime.UtcNow < handoffDeadline)
                {
                    await Task.Delay(Random.Shared.Next(10, 26), cancellationToken);
                }
                catch (OrchestrationCallbackNotReadyException) when (DateTime.UtcNow < handoffDeadline)
                {
                    await Task.Delay(Random.Shared.Next(10, 26), cancellationToken);
                }
            }
        }

        internal static async Task OrchestrateBatchWhenDispatchIsReadyAsync(
            ISagaEngine sagaEngine,
            IReadOnlyCollection<WorkItem> workItems,
            CancellationToken cancellationToken,
            TimeSpan? directHandoffWindow = null)
        {
            ArgumentNullException.ThrowIfNull(sagaEngine);
            ArgumentNullException.ThrowIfNull(workItems);
            if (workItems.Count == 0)
                return;

            if (workItems.Count == 1 || sagaEngine is not IBatchedSagaEngine batchEngine)
            {
                foreach (var workItem in workItems)
                    await OrchestrateWhenDispatchIsReadyAsync(
                        sagaEngine,
                        null,
                        workItem,
                        CreateIntent(workItem),
                        cancellationToken,
                        directHandoffWindow);
                return;
            }

            var intents = workItems.Select(workItem => CreateIntent(workItem)).ToArray();
            var handoffWindow = directHandoffWindow ?? TimeSpan.FromSeconds(2);
            var handoffDeadline = DateTime.UtcNow.Add(handoffWindow);
            while (true)
            {
                try
                {
                    await batchEngine.OrchestrateBatchAsync(intents, cancellationToken);
                    return;
                }
                catch (OrchestrationInstanceLeaseUnavailableException) when (DateTime.UtcNow < handoffDeadline)
                {
                    await Task.Delay(Random.Shared.Next(10, 26), cancellationToken);
                }
                catch (OrchestrationCallbackNotReadyException) when (DateTime.UtcNow < handoffDeadline)
                {
                    await Task.Delay(Random.Shared.Next(10, 26), cancellationToken);
                }
            }
        }

    }
}
