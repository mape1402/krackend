using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Promotion
{
    internal class Promoter : IPromoter
    {
        private readonly IRuntimeArtifactResolver _artifactResolver;
        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly IExecutionTransitionRepository _transitionRepository;
        private readonly IResolvedOrchestrationArtifactAccessor _artifactAccessor;
        private readonly IOrchestrationPayloadState _payloadState;
        private readonly ITriggerPayloadValidator _triggerPayloadValidator;

        public Promoter(
            IRuntimeArtifactResolver artifactResolver,
            IOrchestrationInstanceRepository instanceRepository,
            IExecutionTransitionRepository transitionRepository,
            IResolvedOrchestrationArtifactAccessor artifactAccessor,
            IOrchestrationPayloadState payloadState,
            ITriggerPayloadValidator triggerPayloadValidator)
        {
            _artifactResolver = artifactResolver ?? throw new ArgumentNullException(nameof(artifactResolver));
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
            _artifactAccessor = artifactAccessor ?? throw new ArgumentNullException(nameof(artifactAccessor));
            _payloadState = payloadState ?? throw new ArgumentNullException(nameof(payloadState));
            _triggerPayloadValidator = triggerPayloadValidator ?? throw new ArgumentNullException(nameof(triggerPayloadValidator));
        }

        public async Task<PromotionResult> PromoteToInstanceAsync(PromotionRequest request, CancellationToken cancellationToken = default)
        {
            var resolvedArtifact = await _artifactResolver.ResolveAsync(request.ArtifactId, cancellationToken);
            _artifactAccessor.Set(resolvedArtifact);
            var validationResult = await _triggerPayloadValidator.ValidateAsync(
                resolvedArtifact,
                request.Payload,
                cancellationToken);
            if (!validationResult.Succeeded)
            {
                return new PromotionResult
                {
                    Success = false,
                    ErrorMessage = string.IsNullOrWhiteSpace(validationResult.ErrorMessage)
                        ? "Trigger payload validation failed."
                        : validationResult.ErrorMessage
                };
            }

            var now = DateTime.UtcNow;
            var instanceId = Id.New();
            var correlationId = string.IsNullOrWhiteSpace(request.MessageMetadata?.CorrelationId)
                ? instanceId.ToString()
                : request.MessageMetadata.CorrelationId;
            var sagaId = string.IsNullOrWhiteSpace(request.MessageMetadata?.SagaId)
                ? instanceId.ToString()
                : request.MessageMetadata.SagaId;

            var instance = new OrchestrationInstance
            {
                Id = instanceId,
                OrchestrationDefinitionKey = resolvedArtifact.RuntimeArtifact.OrchestrationDefinitionKey,
                RuntimeOrchestrationArtifactId = resolvedArtifact.RuntimeArtifact.Id,
                TriggerIntakeId = default,
                CorrelationId = correlationId,
                ExecutionKey = $"{resolvedArtifact.Artifact.Key}:{instanceId}",
                Status = OrchestrationInstanceStatus.Created,
                CurrentStageKey = string.Empty,
                CurrentTaskKey = string.Empty,
                CurrentParallelGroupKey = string.Empty,
                StartedOnUtc = now,
                LastUpdatedOnUtc = now,
                FinalOutcome = string.Empty,
                ErrorSummary = string.Empty,
                SnapshotPayload = _payloadState.CreateInitialPayload(request.Payload)
            };

            await _instanceRepository.Create(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                TransitionType = "InstancePromoted",
                FromStatus = string.Empty,
                ToStatus = OrchestrationInstanceStatus.Created.ToString(),
                OccurredOnUtc = now,
                Message = "Mule work item promoted to orchestration instance.",
                Payload = request.Payload?.DeepClone(),
                ProducedBy = nameof(Promoter)
            }, cancellationToken);

            return new PromotionResult
            {
                Success = true,
                SagaId = sagaId,
                InstanceId = instance.Id.ToString()
            };
        }
    }
}
