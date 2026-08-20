using Krackend.Sagas.Orchestrations.Runtime.Engine.Control;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Promotion;
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
        private readonly ILogger<SagaEngine> _logger;

        public SagaEngine(
            IPromoter promoter,
            IDecisionControl decisionControl,
            IDecisionExecutor decisionExecutor,
            ILogger<SagaEngine> logger)
        {
            _promoter = promoter ?? throw new ArgumentNullException(nameof(promoter));
            _decisionControl = decisionControl ?? throw new ArgumentNullException(nameof(decisionControl));
            _decisionExecutor = decisionExecutor ?? throw new ArgumentNullException(nameof(decisionExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartOrchestrationAsync(StartIntent intent, CancellationToken cancellationToken = default)
        {
            var promotionRequest = new PromotionRequest
            {
                ArtifactId = intent.ArtifactId,
                Metadata = intent.Metadata,
                Payload = intent.Payload
            };
            var promotionResult = await _promoter.PromoteToInstanceAsync(promotionRequest, cancellationToken);

            if (!promotionResult.Success)
            {
                _logger.LogWarning("Cannot promote a new SAGA instance with artifact id '{id}': {message}", intent.ArtifactId, promotionResult.ErrorMessage);
                return;
            }

            var metadata = intent.Metadata ?? new InstanceMetadata();
            metadata.SagaId = promotionResult.SagaId;
            metadata.OrchestrationInstanceId = promotionResult.InstanceId;
            metadata.CorrelationId = string.IsNullOrWhiteSpace(metadata.CorrelationId)
                ? promotionResult.InstanceId
                : metadata.CorrelationId;

            var forwardIntent = new ForwardIntent
            {
                ArtifactId = intent.ArtifactId,
                IngressTransport = intent.IngressTransport,
                Metadata = metadata,
                Payload = intent.Payload
            };

            await InternalOrchestrateAsync(forwardIntent, cancellationToken);
        }

        public Task OrchestrateAsync(ForwardIntent intent, CancellationToken cancellationToken = default)
            => InternalOrchestrateAsync(intent, cancellationToken);

        private async Task InternalOrchestrateAsync(ForwardIntent intent, CancellationToken cancellationToken = default)
        {
            for (var cycle = 0; cycle < MaxDecisionCycles; cycle++)
            {
                var decisionRequest = new DecisionRequest
                {
                    ArtifactId = intent.ArtifactId,
                    Metadata = intent.Metadata,
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
            }

            _logger.LogWarning("SAGA engine reached the maximum decision cycles for artifact id '{id}'.", intent.ArtifactId);
        }
    }
}
