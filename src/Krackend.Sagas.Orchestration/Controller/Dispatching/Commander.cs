namespace Krackend.Sagas.Orchestration.Controller.Dispatching
{
    using Krackend.Sagas.Orchestration.Controller.Configuration.Metadata;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provides an implementation for executing commands in saga orchestration.
    /// </summary>
    internal class Commander : ICommander
    {
        /// <inheritdoc/>
        public Task CommandAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            if(command == null) 
                throw new ArgumentNullException(nameof(command));

            var state = command.State;
            var roadmapManager = state.Services.GetRequiredService<IRoadmapManager>();

            var execution = roadmapManager.GetStepExecution(state.OrchestrationKey, state.CurrentStage, state.SagaState);
            return execution.ProcessAsync(command.State, cancellationToken);
        }
    }
}
