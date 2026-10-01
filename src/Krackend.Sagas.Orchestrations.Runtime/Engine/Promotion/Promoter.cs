using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Metadata;

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
        private readonly IOrchestrationPropagationMetadataStore _propagationMetadataStore;

        public Promoter(
            IRuntimeArtifactResolver artifactResolver,
            IOrchestrationInstanceRepository instanceRepository,
            IExecutionTransitionRepository transitionRepository,
            IResolvedOrchestrationArtifactAccessor artifactAccessor,
            IOrchestrationPayloadState payloadState,
            ITriggerPayloadValidator triggerPayloadValidator,
            IOrchestrationPropagationMetadataStore propagationMetadataStore = null)
        {
            _artifactResolver = artifactResolver ?? throw new ArgumentNullException(nameof(artifactResolver));
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
            _artifactAccessor = artifactAccessor ?? throw new ArgumentNullException(nameof(artifactAccessor));
            _payloadState = payloadState ?? throw new ArgumentNullException(nameof(payloadState));
            _triggerPayloadValidator = triggerPayloadValidator ?? throw new ArgumentNullException(nameof(triggerPayloadValidator));
            _propagationMetadataStore = propagationMetadataStore ?? new DefaultOrchestrationPropagationMetadataStore();
        }

        public async Task<PromotionResult> PromoteToInstanceAsync(PromotionRequest request, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var triggerMetadata = GetTriggerMetadata(request.PropagationMetadata);
            var startIdempotencyKey = ResolveStartIdempotencyKey(request, triggerMetadata);
            if (!string.IsNullOrWhiteSpace(startIdempotencyKey))
            {
                var existingInstance = await _instanceRepository.TryGetByStartIdempotencyKey(
                    startIdempotencyKey,
                    cancellationToken);
                if (existingInstance is not null)
                {
                    return FromExistingInstance(existingInstance);
                }
            }

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

            var instanceId = Id.New();
            var correlationId = FirstNonEmpty(triggerMetadata.CorrelationId, request.MessageMetadata?.CorrelationId)
                ?? Id.New().ToString();
            var sagaId = string.IsNullOrWhiteSpace(request.MessageMetadata?.SagaId)
                ? instanceId.ToString()
                : request.MessageMetadata.SagaId;

            var instance = new OrchestrationInstance
            {
                Id = instanceId,
                OrchestrationDefinitionKey = resolvedArtifact.RuntimeArtifact.OrchestrationDefinitionKey,
                RuntimeOrchestrationArtifactId = resolvedArtifact.RuntimeArtifact.Id,
                TriggerIntakeId = default,
                StartIdempotencyKey = startIdempotencyKey,
                CorrelationId = correlationId,
                SagaId = sagaId,
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
            AddTriggerMetadataDiagnostics(instance, triggerMetadata);
            Add(instance, "StartIdempotencyKey", startIdempotencyKey);
            _propagationMetadataStore.Save(instance, request.PropagationMetadata);

            try
            {
                await _instanceRepository.Create(instance, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException &&
                !string.IsNullOrWhiteSpace(startIdempotencyKey))
            {
                var existingInstance = await _instanceRepository.TryGetByStartIdempotencyKey(
                    startIdempotencyKey,
                    cancellationToken);
                if (existingInstance is not null)
                {
                    return FromExistingInstance(existingInstance);
                }

                throw;
            }

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
                CorrelationId = correlationId,
                InstanceId = instance.Id.ToString()
            };
        }

        private static PromotionResult FromExistingInstance(OrchestrationInstance instance)
            => new()
            {
                Success = true,
                SagaId = instance.SagaId,
                CorrelationId = instance.CorrelationId,
                InstanceId = instance.Id.ToString(),
                AlreadyPromoted = true
            };

        private static OrchestrationTriggerMetadata GetTriggerMetadata(OrchestrationPropagationMetadata propagationMetadata)
        {
            if (propagationMetadata?.Items is null ||
                (!propagationMetadata.Items.TryGetValue(OrchestrationMetadataConstants.TriggerMetadataKey, out var payload) &&
                 !propagationMetadata.Items.TryGetValue(OrchestrationMetadataConstants.LegacyTriggerMetadataKey, out payload)))
            {
                return new OrchestrationTriggerMetadata();
            }

            return OrchestrationTriggerMetadata.FromJson(payload);
        }

        private static string ResolveStartIdempotencyKey(
            PromotionRequest request,
            OrchestrationTriggerMetadata triggerMetadata)
        {
            if (!string.IsNullOrWhiteSpace(request.StartIdempotencyKey))
            {
                return request.StartIdempotencyKey.Trim();
            }

            if (!string.IsNullOrWhiteSpace(triggerMetadata.IdempotencyKey))
            {
                return $"trigger:{request.ArtifactId}:{triggerMetadata.IdempotencyKey.Trim()}";
            }

            return null;
        }

        private static void AddTriggerMetadataDiagnostics(
            OrchestrationInstance instance,
            OrchestrationTriggerMetadata triggerMetadata)
        {
            Add(instance, "TriggerTraceId", triggerMetadata.TraceId);
            Add(instance, "TriggerEventId", triggerMetadata.EventId);
            Add(instance, "TriggerEventType", triggerMetadata.EventType);
            Add(instance, "TriggerIdempotencyKey", triggerMetadata.IdempotencyKey);
            Add(instance, "TriggerAggregateId", triggerMetadata.AggregateId);
            Add(instance, "TriggerAggregateType", triggerMetadata.AggregateType);
            Add(instance, "TriggerCausationId", triggerMetadata.CausationId);
        }

        private static void Add(OrchestrationInstance instance, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                instance.Metadata[key] = System.Text.Json.Nodes.JsonValue.Create(value);
            }
        }

        private static string FirstNonEmpty(params string[] values)
            => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
