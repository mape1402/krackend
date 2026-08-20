using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control
{
    internal sealed class DecisionExecutor : IDecisionExecutor
    {
        private readonly IServiceProvider _serviceProvider;

        public DecisionExecutor(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task ExecuteAsync(IDecision decision, CancellationToken cancellationToken = default)
        {
            var handlerType = typeof(IDecisionHandler<>).MakeGenericType(decision.GetType());
            var handler = _serviceProvider.GetRequiredService(handlerType);
            var handleMethod = handlerType.GetMethod(nameof(IDecisionHandler<IDecision>.HandleAsync))
                ?? throw new InvalidOperationException($"Decision handler for '{decision.Kind}' does not expose HandleAsync.");

            var task = (Task)handleMethod.Invoke(handler, [decision, cancellationToken]);
            await task.ConfigureAwait(false);
        }
    }
}
