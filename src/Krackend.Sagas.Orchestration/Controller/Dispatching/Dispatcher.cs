namespace Krackend.Sagas.Orchestration.Controller.Dispatching
{
    using Krackend.Sagas.Orchestration.Controller.Shared;
    using Krackend.Sagas.Orchestration.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Pigeon.Messaging.Contracts;
    using Spider.Pipelines.Core;

    /// <summary>
    /// Provides an implementation for dispatching messages and advancing saga execution.
    /// </summary>
    internal class Dispatcher : IDispatcher
    {
        private readonly IStateManager _stateManager;

        public Dispatcher(IStateManager stateManager)
        {
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        }

        /// <inheritdoc/>
        public async Task StartAsync(string topic, SemanticVersion version, string payload, CancellationToken cancellationToken = default)
        {
            var currentState = await _stateManager.CreateState(topic);
            currentState.SetPayload(payload);

            await DirectAsync(currentState, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task ForwardAsync(string payload, CancellationToken cancellationToken = default)
        {
            var currentState = await _stateManager.GetCurrentState();
            currentState.SetPayload(payload);

            await DirectAsync(currentState, cancellationToken);
        }

        private Task DirectAsync(SagaExecutionState state, CancellationToken cancellationToken = default)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            var command = new Command(state);

            var spider = state.Services.GetRequiredService<ISpider>();

            return spider.InitBridge<ICommander>()
                    .Attach<ICommand>(pipeline =>
                    {
                        pipeline
                            .OnPreProcess(before =>
                            {
                                before
                                    .OnPreProcess((ctx, args) =>
                                    {
                                        var command = ctx.Request;
                                        command.State.CloseAndMoveStage();
                                        return Task.CompletedTask;
                                    });
                            })
                            .OnPostProcess(after =>
                            {
                                after
                                    .OnSuccess((ctx, args) =>
                                    {
                                        var command = ctx.Request;
                                        command.State.StartCurrentStage();
                                        return Task.CompletedTask;
                                    })
                                    .OnSuccess((ctx, args) =>
                                    {
                                        var state = ctx.Request.State;
                                        var metadata = new OrchestrationMetadata
                                        {
                                            SagaId = state.SagaId,
                                            CurrentStage = state.CurrentStage,
                                            NextStage = state.NextStage,
                                            PreviousStage = state.PreviousStage,
                                            IsCompensation = state.SagaState == SagaState.Backward,
                                            MessageType = MessageType.Command,
                                            OrchestrationKey = state.OrchestrationKey,
                                            OrchestrationName = state.OrchestrationName,
                                            OrchestrationTopic = state.GetOrchestrationTopic(),
                                            OrchestrationTopicVersion = state.GetOrchestrationTopicVersion(),
                                            Timestamp = DateTime.UtcNow,
                                            RetryCount = 0,
                                            CausationId = "", // TODO: Set this properly if needed
                                            CorrelationId = "", // TODO: Set this properly if needed
                                            CorrelationSource = "", // TODO: Set this properly if needed
                                            MessageId = "", // TODO: Set this properly if needed
                                            MaxRetries = 3, // Default value, can be configured
                                        };

                                        var setter = ctx.Services.GetRequiredService<IMetadataSetter>();
                                        setter.SetMetadata(metadata);

                                        return Task.CompletedTask;
                                    });
                            });
                    })
                    .ExecuteAsync(commander => commander.CommandAsync, command, cancellationToken);
        }
    }
}
