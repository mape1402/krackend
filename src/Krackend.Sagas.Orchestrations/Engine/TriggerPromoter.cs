using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Engine;

public sealed class TriggerPromoter : ITriggerPromoter
{
    private readonly IArtifactResolver _artifactResolver;
    private readonly ITriggerIntakeRepository _intakeRepository;
    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IExecutionTransitionRepository _timelineRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerPromoter"/> class.
    /// </summary>
    /// <param name="artifactResolver">Resolver used to find the active runtime artifact.</param>
    /// <param name="intakeRepository">Repository used to persist trigger intake records.</param>
    /// <param name="instanceRepository">Repository used to persist orchestration instances.</param>
    /// <param name="timelineRepository">Repository used to write execution timeline transitions.</param>
    public TriggerPromoter(IArtifactResolver artifactResolver, ITriggerIntakeRepository intakeRepository, IOrchestrationInstanceRepository instanceRepository, IExecutionTransitionRepository timelineRepository)
    {
        _artifactResolver = artifactResolver ?? throw new ArgumentNullException(nameof(artifactResolver));
        _intakeRepository = intakeRepository ?? throw new ArgumentNullException(nameof(intakeRepository));
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _timelineRepository = timelineRepository ?? throw new ArgumentNullException(nameof(timelineRepository));
    }

    /// <inheritdoc/>
    public async Task<TriggerPromotionResult> Promote(TriggerIntakeBufferItem item, CancellationToken cancellationToken = default)
    {
        if (item is null)
            throw new ArgumentNullException(nameof(item));

        var profile = RuntimeProfile.Start("runtime.promote");
        var payload = ParsePayload(item.PayloadJson);
        profile.Mark("parse-payload");
        var existing = await ResolveExistingIntake(item, cancellationToken);
        profile.Mark("resolve-existing-intake");

        if (existing?.PromotedInstanceId is not null)
        {
            var result = await CreateExistingPromotionResult(item, existing, cancellationToken);
            profile.Mark("existing-promotion");
            profile.Stop();
            return result;
        }

        var artifact = await _artifactResolver.Resolve(item.EnvironmentKey, item.TriggerKey, item.ArtifactVersion, cancellationToken);
        profile.Mark("resolve-artifact");

        var now = DateTime.UtcNow;
        var intake = existing ?? new TriggerIntake
        {
            Id = Id.New(),
            TriggerType = item.TriggerType,
            TriggerKey = item.TriggerKey.Trim(),
            EnvironmentKey = item.EnvironmentKey.Trim(),
            CorrelationId = NormalizeCorrelationId(item),
            IdempotencyKey = item.IdempotencyKey ?? string.Empty,
            SourceMessageId = item.SourceMessageId ?? string.Empty,
            SourceRequestId = item.BufferItemId.ToString(),
            RawPayload = payload.DeepClone(),
            NormalizedPayload = payload.DeepClone(),
            Status = TriggerIntakeStatus.PersistedPrimary,
            PersistenceLevel = "Primary",
            BufferLocation = "InMemory",
            ResolvedArtifactId = artifact.Id,
            ReceivedOnUtc = item.ReceivedOnUtc == default ? now : item.ReceivedOnUtc
        };

        if (existing is null)
            await _intakeRepository.Create(intake, cancellationToken);
        profile.Mark("create-intake");

        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            EnvironmentKey = item.EnvironmentKey.Trim(),
            OrchestrationDefinitionKey = item.TriggerKey.Trim(),
            RuntimeOrchestrationArtifactId = artifact.Id,
            TriggerIntakeId = intake.Id,
            CorrelationId = intake.CorrelationId,
            ExecutionKey = BuildExecutionKey(item.TriggerKey, intake),
            Status = OrchestrationInstanceStatus.Created,
            StartedOnUtc = now,
            LastUpdatedOnUtc = now,
            SnapshotPayload = payload.DeepClone(),
            Metadata = new Dictionary<string, JsonNode>
            {
                ["triggerType"] = item.TriggerType.ToString(),
                ["bufferItemId"] = item.BufferItemId.ToString(),
                ["artifactVersion"] = artifact.Version.ToString()
            }
        };

        try
        {
            await _instanceRepository.Create(instance, cancellationToken);
            profile.Mark("create-instance");
        }
        catch when (!string.IsNullOrWhiteSpace(intake.IdempotencyKey))
        {
            var existingPromotion = await TryResolveConcurrentPromotion(item, cancellationToken);
            if (existingPromotion is not null)
                return existingPromotion;

            throw;
        }

        intake.Status = TriggerIntakeStatus.PromotedToRuntime;
        intake.ResolvedArtifactId = artifact.Id;
        intake.PromotedInstanceId = instance.Id;
        intake.PromotedOnUtc = now;
        await _intakeRepository.Update(intake, cancellationToken);
        profile.Mark("update-intake");

        await _timelineRepository.Create(new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            TransitionType = "TriggerPromoted",
            FromStatus = TriggerIntakeStatus.PersistedPrimary.ToString(),
            ToStatus = TriggerIntakeStatus.PromotedToRuntime.ToString(),
            OccurredOnUtc = now,
            Message = "Trigger intake promoted to runtime instance.",
            ProducedBy = "Krackend.Sagas.Orchestrations.Engine",
            Payload = payload.DeepClone()
        }, cancellationToken);
        profile.Mark("transition");
        profile.Stop();

        return new TriggerPromotionResult
        {
            Intake = intake,
            Instance = instance,
            Artifact = artifact
        };
    }

    private async Task<TriggerPromotionResult> CreateExistingPromotionResult(TriggerIntakeBufferItem item, TriggerIntake existing, CancellationToken cancellationToken)
    {
        var existingArtifact = existing.ResolvedArtifactId is null
            ? await _artifactResolver.Resolve(item.EnvironmentKey, existing.TriggerKey, item.ArtifactVersion, cancellationToken)
            : await _artifactResolver.ResolveById(existing.ResolvedArtifactId.Value, cancellationToken);
        var existingInstance = await _instanceRepository.GetById(existing.PromotedInstanceId.Value, cancellationToken);

        return new TriggerPromotionResult
        {
            Intake = existing,
            Instance = existingInstance,
            Artifact = existingArtifact
        };
    }

    private async Task<TriggerPromotionResult> TryResolveConcurrentPromotion(TriggerIntakeBufferItem item, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var existing = await ResolveExistingIntake(item, cancellationToken);
            if (existing?.PromotedInstanceId is not null)
                return await CreateExistingPromotionResult(item, existing, cancellationToken);

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        return null;
    }

    private Task<TriggerIntake> ResolveExistingIntake(TriggerIntakeBufferItem item, CancellationToken cancellationToken)
    {
        return string.IsNullOrWhiteSpace(item.IdempotencyKey)
            ? Task.FromResult<TriggerIntake>(null)
            : _intakeRepository.GetByIdempotencyKey(item.EnvironmentKey, item.IdempotencyKey, cancellationToken);
    }

    private static JsonNode ParsePayload(string payloadJson)
    {
        try
        {
            return JsonNode.Parse(payloadJson) ?? new JsonObject();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Trigger payload is not valid JSON: {ex.Message}", ex);
        }
    }

    private static string NormalizeCorrelationId(TriggerIntakeBufferItem item)
        => string.IsNullOrWhiteSpace(item.CorrelationId)
            ? item.BufferItemId.ToString()
            : item.CorrelationId.Trim();

    private static string BuildExecutionKey(string triggerKey, TriggerIntake intake)
        => string.IsNullOrWhiteSpace(intake.IdempotencyKey)
            ? $"{triggerKey.Trim()}::{intake.CorrelationId}::{intake.Id}"
            : $"{triggerKey.Trim()}::{intake.CorrelationId}::idempotency:{Hash(intake.EnvironmentKey, intake.IdempotencyKey)}";

    private static string Hash(string environmentKey, string idempotencyKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{environmentKey.Trim()}::{idempotencyKey.Trim()}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
