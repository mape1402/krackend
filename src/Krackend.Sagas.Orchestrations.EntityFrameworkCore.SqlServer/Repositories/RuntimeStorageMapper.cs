using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Entities;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Repositories;

internal static class RuntimeStorageMapper
{
    public static RuntimeArtifactEntity ToEntity(RuntimeOrchestrationArtifact x) => new()
    {
        Id = x.Id,
        EnvironmentKey = x.EnvironmentKey,
        OrchestrationDefinitionKey = x.OrchestrationDefinitionKey,
        ArtifactType = x.ArtifactType,
        SourceOrchestrationVersionId = x.SourceOrchestrationVersionId,
        Version = x.Version.ToString(),
        ArtifactChecksum = x.ArtifactChecksum.Value,
        ArtifactPayloadJson = ToJson(x.ArtifactPayload),
        IsActive = x.IsActive,
        LoadedToCache = x.LoadedToCache,
        DeployedOnUtc = x.DeployedOnUtc,
        ActivatedOnUtc = x.ActivatedOnUtc,
        RetiredOnUtc = x.RetiredOnUtc,
        SupersededByArtifactId = x.SupersededByArtifactId,
        Notes = x.Notes
    };

    public static RuntimeOrchestrationArtifact ToDomain(RuntimeArtifactEntity x) => new()
    {
        Id = x.Id,
        EnvironmentKey = x.EnvironmentKey,
        OrchestrationDefinitionKey = x.OrchestrationDefinitionKey,
        ArtifactType = x.ArtifactType,
        SourceOrchestrationVersionId = x.SourceOrchestrationVersionId,
        Version = ParseVersion(x.Version),
        ArtifactChecksum = new Checksum(x.ArtifactChecksum),
        ArtifactPayload = ParseJson(x.ArtifactPayloadJson),
        IsActive = x.IsActive,
        LoadedToCache = x.LoadedToCache,
        DeployedOnUtc = x.DeployedOnUtc,
        ActivatedOnUtc = x.ActivatedOnUtc,
        RetiredOnUtc = x.RetiredOnUtc,
        SupersededByArtifactId = x.SupersededByArtifactId,
        Notes = x.Notes
    };

    public static TriggerIntakeEntity ToEntity(TriggerIntake x) => new()
    {
        Id = x.Id,
        TriggerType = x.TriggerType,
        TriggerKey = x.TriggerKey,
        EnvironmentKey = x.EnvironmentKey,
        CorrelationId = x.CorrelationId,
        IdempotencyKey = x.IdempotencyKey,
        SourceMessageId = x.SourceMessageId,
        SourceRequestId = x.SourceRequestId,
        RawPayloadJson = ToJson(x.RawPayload),
        NormalizedPayloadJson = ToJson(x.NormalizedPayload),
        Status = x.Status,
        PersistenceLevel = x.PersistenceLevel,
        BufferLocation = x.BufferLocation,
        ResolvedArtifactId = x.ResolvedArtifactId,
        PromotedInstanceId = x.PromotedInstanceId,
        ReceivedOnUtc = x.ReceivedOnUtc,
        PromotedOnUtc = x.PromotedOnUtc,
        ExpiresOnUtc = x.ExpiresOnUtc,
        RejectionReason = x.RejectionReason,
        FailureReason = x.FailureReason
    };

    public static TriggerIntake ToDomain(TriggerIntakeEntity x) => new()
    {
        Id = x.Id,
        TriggerType = x.TriggerType,
        TriggerKey = x.TriggerKey,
        EnvironmentKey = x.EnvironmentKey,
        CorrelationId = x.CorrelationId,
        IdempotencyKey = x.IdempotencyKey,
        SourceMessageId = x.SourceMessageId,
        SourceRequestId = x.SourceRequestId,
        RawPayload = ParseJson(x.RawPayloadJson),
        NormalizedPayload = ParseJson(x.NormalizedPayloadJson),
        Status = x.Status,
        PersistenceLevel = x.PersistenceLevel,
        BufferLocation = x.BufferLocation,
        ResolvedArtifactId = x.ResolvedArtifactId,
        PromotedInstanceId = x.PromotedInstanceId,
        ReceivedOnUtc = x.ReceivedOnUtc,
        PromotedOnUtc = x.PromotedOnUtc,
        ExpiresOnUtc = x.ExpiresOnUtc,
        RejectionReason = x.RejectionReason,
        FailureReason = x.FailureReason
    };

    public static TriggerIntakeAttemptEntity ToEntity(TriggerIntakeAttempt x) => new()
    {
        Id = x.Id,
        TriggerIntakeId = x.TriggerIntakeId,
        AttemptNumber = x.AttemptNumber,
        ActionType = x.ActionType,
        Outcome = x.Outcome,
        StartedOnUtc = x.StartedOnUtc,
        FinishedOnUtc = x.FinishedOnUtc,
        ErrorCode = x.ErrorCode,
        ErrorMessage = x.ErrorMessage,
        MetadataJson = ToJson(x.Metadata)
    };

    public static TriggerIntakeAttempt ToDomain(TriggerIntakeAttemptEntity x) => new()
    {
        Id = x.Id,
        TriggerIntakeId = x.TriggerIntakeId,
        AttemptNumber = x.AttemptNumber,
        ActionType = x.ActionType,
        Outcome = x.Outcome,
        StartedOnUtc = x.StartedOnUtc,
        FinishedOnUtc = x.FinishedOnUtc,
        ErrorCode = x.ErrorCode,
        ErrorMessage = x.ErrorMessage,
        Metadata = ParseJsonDictionary(x.MetadataJson)
    };

    public static OrchestrationInstanceEntity ToEntity(OrchestrationInstance x) => new()
    {
        Id = x.Id,
        EnvironmentKey = x.EnvironmentKey,
        OrchestrationDefinitionKey = x.OrchestrationDefinitionKey,
        RuntimeOrchestrationArtifactId = x.RuntimeOrchestrationArtifactId,
        TriggerIntakeId = x.TriggerIntakeId,
        CorrelationId = x.CorrelationId,
        ExecutionKey = x.ExecutionKey,
        Status = x.Status,
        CurrentStageKey = x.CurrentStageKey,
        CurrentTaskKey = x.CurrentTaskKey,
        CurrentParallelGroupKey = x.CurrentParallelGroupKey,
        StartedOnUtc = x.StartedOnUtc,
        LastUpdatedOnUtc = x.LastUpdatedOnUtc,
        WaitingSinceUtc = x.WaitingSinceUtc,
        CompletedOnUtc = x.CompletedOnUtc,
        FailedOnUtc = x.FailedOnUtc,
        StoppedOnUtc = x.StoppedOnUtc,
        CompensationStartedOnUtc = x.CompensationStartedOnUtc,
        CompensatedOnUtc = x.CompensatedOnUtc,
        FinalOutcome = x.FinalOutcome,
        ErrorSummary = x.ErrorSummary,
        RetryCount = x.RetryCount,
        ActiveLeaseId = x.ActiveLeaseId,
        ActiveLeaseExpiresOnUtc = x.ActiveLeaseExpiresOnUtc,
        SnapshotPayloadJson = ToJson(x.SnapshotPayload),
        MetadataJson = ToJson(x.Metadata)
    };

    public static OrchestrationInstance ToDomain(OrchestrationInstanceEntity x) => new()
    {
        Id = x.Id,
        EnvironmentKey = x.EnvironmentKey,
        OrchestrationDefinitionKey = x.OrchestrationDefinitionKey,
        RuntimeOrchestrationArtifactId = x.RuntimeOrchestrationArtifactId,
        TriggerIntakeId = x.TriggerIntakeId,
        CorrelationId = x.CorrelationId,
        ExecutionKey = x.ExecutionKey,
        Status = x.Status,
        CurrentStageKey = x.CurrentStageKey,
        CurrentTaskKey = x.CurrentTaskKey,
        CurrentParallelGroupKey = x.CurrentParallelGroupKey,
        StartedOnUtc = x.StartedOnUtc,
        LastUpdatedOnUtc = x.LastUpdatedOnUtc,
        WaitingSinceUtc = x.WaitingSinceUtc,
        CompletedOnUtc = x.CompletedOnUtc,
        FailedOnUtc = x.FailedOnUtc,
        StoppedOnUtc = x.StoppedOnUtc,
        CompensationStartedOnUtc = x.CompensationStartedOnUtc,
        CompensatedOnUtc = x.CompensatedOnUtc,
        FinalOutcome = x.FinalOutcome,
        ErrorSummary = x.ErrorSummary,
        RetryCount = x.RetryCount,
        ActiveLeaseId = x.ActiveLeaseId,
        ActiveLeaseExpiresOnUtc = x.ActiveLeaseExpiresOnUtc,
        SnapshotPayload = ParseJson(x.SnapshotPayloadJson),
        Metadata = ParseJsonDictionary(x.MetadataJson)
    };

    public static StageExecutionEntity ToEntity(StageExecution x) => new()
    {
        Id = x.Id,
        OrchestrationInstanceId = x.OrchestrationInstanceId,
        StageKey = x.StageKey,
        Order = x.Order,
        Status = x.Status,
        WasSkipped = x.WasSkipped,
        SkipReason = x.SkipReason,
        ExecutionConditionResult = x.ExecutionConditionResult,
        StartedOnUtc = x.StartedOnUtc,
        CompletedOnUtc = x.CompletedOnUtc,
        FailedOnUtc = x.FailedOnUtc,
        ErrorSummary = x.ErrorSummary,
        ParallelGroupCount = x.ParallelGroupCount,
        MetadataJson = ToJson(x.Metadata)
    };

    public static StageExecution ToDomain(StageExecutionEntity x) => new()
    {
        Id = x.Id,
        OrchestrationInstanceId = x.OrchestrationInstanceId,
        StageKey = x.StageKey,
        Order = x.Order,
        Status = x.Status,
        WasSkipped = x.WasSkipped,
        SkipReason = x.SkipReason,
        ExecutionConditionResult = x.ExecutionConditionResult,
        StartedOnUtc = x.StartedOnUtc,
        CompletedOnUtc = x.CompletedOnUtc,
        FailedOnUtc = x.FailedOnUtc,
        ErrorSummary = x.ErrorSummary,
        ParallelGroupCount = x.ParallelGroupCount,
        Metadata = ParseJsonDictionary(x.MetadataJson)
    };

    public static TaskExecutionEntity ToEntity(TaskExecution x) => new()
    {
        Id = x.Id,
        OrchestrationInstanceId = x.OrchestrationInstanceId,
        StageExecutionId = x.StageExecutionId,
        TaskKey = x.TaskKey,
        TaskKind = x.TaskKind,
        ExecutionMode = x.ExecutionMode,
        ParallelGroupId = x.ParallelGroupId,
        Status = x.Status,
        WasSkipped = x.WasSkipped,
        SkipReason = x.SkipReason,
        ExecutionConditionResult = x.ExecutionConditionResult,
        OnErrorPolicy = x.OnErrorPolicy,
        AwaitResponse = x.AwaitResponse,
        StartedOnUtc = x.StartedOnUtc,
        WaitingSinceUtc = x.WaitingSinceUtc,
        CompletedOnUtc = x.CompletedOnUtc,
        FailedOnUtc = x.FailedOnUtc,
        TimedOutOnUtc = x.TimedOutOnUtc,
        LastAttemptNumber = x.LastAttemptNumber,
        OutputVariablesPayloadJson = ToJson(x.OutputVariablesPayload),
        CorrelationId = x.CorrelationId,
        MetadataJson = ToJson(x.Metadata)
    };

    public static TaskExecution ToDomain(TaskExecutionEntity x) => new()
    {
        Id = x.Id,
        OrchestrationInstanceId = x.OrchestrationInstanceId,
        StageExecutionId = x.StageExecutionId,
        TaskKey = x.TaskKey,
        TaskKind = x.TaskKind,
        ExecutionMode = x.ExecutionMode,
        ParallelGroupId = x.ParallelGroupId,
        Status = x.Status,
        WasSkipped = x.WasSkipped,
        SkipReason = x.SkipReason,
        ExecutionConditionResult = x.ExecutionConditionResult,
        OnErrorPolicy = x.OnErrorPolicy,
        AwaitResponse = x.AwaitResponse,
        StartedOnUtc = x.StartedOnUtc,
        WaitingSinceUtc = x.WaitingSinceUtc,
        CompletedOnUtc = x.CompletedOnUtc,
        FailedOnUtc = x.FailedOnUtc,
        TimedOutOnUtc = x.TimedOutOnUtc,
        LastAttemptNumber = x.LastAttemptNumber,
        OutputVariablesPayload = ParseJson(x.OutputVariablesPayloadJson),
        CorrelationId = x.CorrelationId,
        Metadata = ParseJsonDictionary(x.MetadataJson)
    };

    public static TaskExecutionAttemptEntity ToEntity(TaskExecutionAttempt x) => new()
    {
        Id = x.Id,
        TaskExecutionId = x.TaskExecutionId,
        AttemptNumber = x.AttemptNumber,
        Status = x.Status,
        StartedOnUtc = x.StartedOnUtc,
        WaitingSinceUtc = x.WaitingSinceUtc,
        CompletedOnUtc = x.CompletedOnUtc,
        FailedOnUtc = x.FailedOnUtc,
        TimedOutOnUtc = x.TimedOutOnUtc,
        RequestPayloadJson = ToJson(x.RequestPayload),
        ResponsePayloadJson = ToJson(x.ResponsePayload),
        ErrorCode = x.ErrorCode,
        ErrorMessage = x.ErrorMessage,
        DispatchId = x.DispatchId,
        MetadataJson = ToJson(x.Metadata)
    };

    public static TaskExecutionAttempt ToDomain(TaskExecutionAttemptEntity x) => new()
    {
        Id = x.Id,
        TaskExecutionId = x.TaskExecutionId,
        AttemptNumber = x.AttemptNumber,
        Status = x.Status,
        StartedOnUtc = x.StartedOnUtc,
        WaitingSinceUtc = x.WaitingSinceUtc,
        CompletedOnUtc = x.CompletedOnUtc,
        FailedOnUtc = x.FailedOnUtc,
        TimedOutOnUtc = x.TimedOutOnUtc,
        RequestPayload = ParseJson(x.RequestPayloadJson),
        ResponsePayload = ParseJson(x.ResponsePayloadJson),
        ErrorCode = x.ErrorCode,
        ErrorMessage = x.ErrorMessage,
        DispatchId = x.DispatchId,
        Metadata = ParseJsonDictionary(x.MetadataJson)
    };

    public static TaskDispatchEntity ToEntity(TaskDispatch x) => new()
    {
        Id = x.Id,
        TaskExecutionAttemptId = x.TaskExecutionAttemptId,
        DispatchType = x.DispatchType,
        Destination = x.Destination,
        RequestPayloadJson = ToJson(x.RequestPayload),
        DispatchStatus = x.DispatchStatus,
        CommandId = x.CommandId,
        CorrelationId = x.CorrelationId,
        SentOnUtc = x.SentOnUtc,
        AcknowledgedOnUtc = x.AcknowledgedOnUtc,
        FailedOnUtc = x.FailedOnUtc,
        FailureReason = x.FailureReason,
        MetadataJson = ToJson(x.Metadata)
    };

    public static TaskDispatch ToDomain(TaskDispatchEntity x) => new()
    {
        Id = x.Id,
        TaskExecutionAttemptId = x.TaskExecutionAttemptId,
        DispatchType = x.DispatchType,
        Destination = x.Destination,
        RequestPayload = ParseJson(x.RequestPayloadJson),
        DispatchStatus = x.DispatchStatus,
        CommandId = x.CommandId,
        CorrelationId = x.CorrelationId,
        SentOnUtc = x.SentOnUtc,
        AcknowledgedOnUtc = x.AcknowledgedOnUtc,
        FailedOnUtc = x.FailedOnUtc,
        FailureReason = x.FailureReason,
        Metadata = ParseJsonDictionary(x.MetadataJson)
    };

    public static CompensationExecutionEntity ToEntity(CompensationExecution x) => new()
    {
        Id = x.Id,
        OrchestrationInstanceId = x.OrchestrationInstanceId,
        SourceTaskExecutionId = x.SourceTaskExecutionId,
        CompensationTaskKey = x.CompensationTaskKey,
        Status = x.Status,
        StartedOnUtc = x.StartedOnUtc,
        CompletedOnUtc = x.CompletedOnUtc,
        FailedOnUtc = x.FailedOnUtc,
        RequestPayloadJson = ToJson(x.RequestPayload),
        ResponsePayloadJson = ToJson(x.ResponsePayload),
        ErrorMessage = x.ErrorMessage,
        MetadataJson = ToJson(x.Metadata)
    };

    public static CompensationExecution ToDomain(CompensationExecutionEntity x) => new()
    {
        Id = x.Id,
        OrchestrationInstanceId = x.OrchestrationInstanceId,
        SourceTaskExecutionId = x.SourceTaskExecutionId,
        CompensationTaskKey = x.CompensationTaskKey,
        Status = x.Status,
        StartedOnUtc = x.StartedOnUtc,
        CompletedOnUtc = x.CompletedOnUtc,
        FailedOnUtc = x.FailedOnUtc,
        RequestPayload = ParseJson(x.RequestPayloadJson),
        ResponsePayload = ParseJson(x.ResponsePayloadJson),
        ErrorMessage = x.ErrorMessage,
        Metadata = ParseJsonDictionary(x.MetadataJson)
    };

    public static ExecutionTransitionEntity ToEntity(ExecutionTransition x) => new()
    {
        Id = x.Id,
        OrchestrationInstanceId = x.OrchestrationInstanceId,
        StageExecutionId = x.StageExecutionId,
        TaskExecutionId = x.TaskExecutionId,
        TaskExecutionAttemptId = x.TaskExecutionAttemptId,
        TransitionType = x.TransitionType,
        FromStatus = x.FromStatus,
        ToStatus = x.ToStatus,
        OccurredOnUtc = x.OccurredOnUtc,
        Message = x.Message,
        PayloadJson = ToJson(x.Payload),
        ProducedBy = x.ProducedBy
    };

    public static ExecutionTransition ToDomain(ExecutionTransitionEntity x) => new()
    {
        Id = x.Id,
        OrchestrationInstanceId = x.OrchestrationInstanceId,
        StageExecutionId = x.StageExecutionId,
        TaskExecutionId = x.TaskExecutionId,
        TaskExecutionAttemptId = x.TaskExecutionAttemptId,
        TransitionType = x.TransitionType,
        FromStatus = x.FromStatus,
        ToStatus = x.ToStatus,
        OccurredOnUtc = x.OccurredOnUtc,
        Message = x.Message,
        Payload = ParseJson(x.PayloadJson),
        ProducedBy = x.ProducedBy
    };

    public static InstanceVariableEntity ToEntity(InstanceVariable x) => new()
    {
        Id = x.Id,
        OrchestrationInstanceId = x.OrchestrationInstanceId,
        Key = x.Key,
        Scope = x.Scope,
        ValueType = x.ValueType,
        ValueJson = ToJson(x.Value),
        IsSensitive = x.IsSensitive,
        SourceType = x.SourceType,
        SourceReference = x.SourceReference,
        CreatedOnUtc = x.CreatedOnUtc,
        UpdatedOnUtc = x.UpdatedOnUtc,
        LastUpdatedBy = x.LastUpdatedBy
    };

    public static InstanceVariable ToDomain(InstanceVariableEntity x) => new()
    {
        Id = x.Id,
        OrchestrationInstanceId = x.OrchestrationInstanceId,
        Key = x.Key,
        Scope = x.Scope,
        ValueType = x.ValueType,
        Value = ParseJson(x.ValueJson),
        IsSensitive = x.IsSensitive,
        SourceType = x.SourceType,
        SourceReference = x.SourceReference,
        CreatedOnUtc = x.CreatedOnUtc,
        UpdatedOnUtc = x.UpdatedOnUtc,
        LastUpdatedBy = x.LastUpdatedBy
    };

    public static EnvironmentVariableEntity ToEntity(EnvironmentVariableValue x) => new()
    {
        Id = x.Id,
        EnvironmentKey = x.EnvironmentKey,
        VariableKey = x.VariableKey,
        ValueType = x.ValueType,
        ValueJson = ToJson(x.Value),
        IsSensitive = x.IsSensitive,
        IsResolved = x.IsResolved,
        LastValidatedOnUtc = x.LastValidatedOnUtc,
        CreatedOnUtc = x.CreatedOnUtc,
        UpdatedOnUtc = x.UpdatedOnUtc,
        UpdatedBy = x.UpdatedBy,
        Notes = x.Notes
    };

    public static EnvironmentVariableValue ToDomain(EnvironmentVariableEntity x) => new()
    {
        Id = x.Id,
        EnvironmentKey = x.EnvironmentKey,
        VariableKey = x.VariableKey,
        ValueType = x.ValueType,
        Value = ParseJson(x.ValueJson),
        IsSensitive = x.IsSensitive,
        IsResolved = x.IsResolved,
        LastValidatedOnUtc = x.LastValidatedOnUtc,
        CreatedOnUtc = x.CreatedOnUtc,
        UpdatedOnUtc = x.UpdatedOnUtc,
        UpdatedBy = x.UpdatedBy,
        Notes = x.Notes
    };

    private static string ToJson(JsonNode value) => value?.ToJsonString();

    private static string ToJson(Dictionary<string, JsonNode> value)
        => value is null || value.Count == 0 ? null : JsonSerializer.Serialize(value);

    private static JsonNode ParseJson(string json)
        => string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json);

    private static Dictionary<string, JsonNode> ParseJsonDictionary(string json)
        => string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, JsonNode>()
            : JsonSerializer.Deserialize<Dictionary<string, JsonNode>>(json) ?? new Dictionary<string, JsonNode>();

    private static SemanticVersion ParseVersion(string value)
    {
        var parts = (value ?? "0.0.0").Split('.');
        return new SemanticVersion(
            parts.Length > 0 && int.TryParse(parts[0], out var major) ? major : 0,
            parts.Length > 1 && int.TryParse(parts[1], out var minor) ? minor : 0,
            parts.Length > 2 && int.TryParse(parts[2], out var patch) ? patch : 0);
    }
}
