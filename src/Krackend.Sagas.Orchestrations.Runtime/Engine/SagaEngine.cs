using Krackend.Sagas.Orchestrations.Runtime.Engine.Control;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Promotion;
using Microsoft.Extensions.Logging;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine
{
    internal class SagaEngine : ISagaEngine
    {
        private readonly IPromoter _promoter;
        private readonly IDecisionControl _decisionControl;
        private readonly IRemoteCommandDispatcher _dispatcher;
        private readonly ILogger<SagaEngine> _logger;

        public SagaEngine(IPromoter promoter, IDecisionControl decisionControl, IRemoteCommandDispatcher dispatcher, ILogger<SagaEngine> logger)
        {
            _promoter = promoter ?? throw new ArgumentNullException(nameof(promoter));
            _decisionControl = decisionControl ?? throw new ArgumentNullException(nameof(decisionControl));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartOrchestrationAsync(StartIntent intent, CancellationToken cancellationToken = default)
        {
            var promotionRequest = new PromotionRequest { ArtifactId = intent.ArtifactId, Payload = intent.Payload };
            var promotionResult = await _promoter.PromoteToInstanceAsync(promotionRequest, cancellationToken);

            if (!promotionResult.Success)
            {
                _logger.LogWarning("Cannot promote a new SAGA instance with artifact id '{id}'", intent.ArtifactId);
                //TODO: implement a log when promotion fails, and should retry or save log directly
                return;
            }

            var forwardIntent = new ForwardIntent
            {
                ArtifactId = intent.ArtifactId,
                IngressTransport = intent.IngressTransport,
                Metadata = intent.Metadata,
                Payload = intent.Payload
            };

            await InternalOrchestrateAsync(forwardIntent, cancellationToken);
        }

        public Task OrchestrateAsync(ForwardIntent intent, CancellationToken cancellationToken = default)
            => InternalOrchestrateAsync(intent, cancellationToken);

        private async Task InternalOrchestrateAsync(ForwardIntent intent, CancellationToken cancellationToken = default)
        {
            var decisionRequest = new DecisionRequest
            {
                ArtifactId = intent.ArtifactId,
                Metadata = intent.Metadata,
                Payload = intent.Payload
            };

            var decisions = await _decisionControl.DecideAsync(decisionRequest, cancellationToken);
            var taskActions = decisions.Select(x => x.HandsOn());

            foreach (var action in taskActions)
                await action.ExecuteAsync(cancellationToken);
        }
    }
}
