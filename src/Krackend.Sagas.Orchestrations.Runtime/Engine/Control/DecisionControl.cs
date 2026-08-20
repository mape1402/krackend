using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control
{
    internal class DecisionControl : IDecisionControl
    {
        private readonly IServiceProvider _serviceProvider;

        public DecisionControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task<IReadOnlyCollection<IDecision>> DecideAsync(DecisionRequest request, CancellationToken cancellationToken)
        {
            // Here connect with storage and take decisions. Forward, mark as failed, compesate, retry, finished, etc.
            await Task.Delay(10);
            return [ new ForwardDecision(new ForwardContext(_serviceProvider)) ];
        }
    }
}
