namespace Krackend.Sagas.Orchestrations.Client.Responses;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Responses;

internal sealed class DefaultOrchestrationClientResponseFactory : IOrchestrationClientResponseFactory
{
    public RuntimeTaskResponseEnvelope Success(
        InstanceMetadata metadata,
        Type requestType,
        Type responseType,
        JsonNode payload,
        TimeSpan? executionTime)
    {
        metadata ??= new InstanceMetadata();

        return new RuntimeTaskResponseEnvelope
        {
            Succeeded = true,
            Status = "Succeeded",
            SagaId = metadata.SagaId,
            OrchestrationInstanceId = metadata.OrchestrationInstanceId,
            CorrelationId = metadata.CorrelationId,
            StageKey = metadata.CurrentStage,
            TaskKey = metadata.CurrentTasks?.FirstOrDefault(),
            TaskExecutionId = metadata.TaskExecutionId,
            DispatchId = metadata.DispatchId,
            Attempt = metadata.Attempt,
            RequestType = requestType?.FullName,
            ResponseType = responseType?.FullName,
            ExecutionTimeMs = executionTime.HasValue ? Convert.ToInt64(executionTime.Value.TotalMilliseconds) : null,
            Payload = payload
        };
    }

    public RuntimeTaskResponseEnvelope Failure(
        InstanceMetadata metadata,
        Type requestType,
        string reason,
        Exception exception,
        TimeSpan? executionTime)
    {
        metadata ??= new InstanceMetadata();

        return new RuntimeTaskResponseEnvelope
        {
            Succeeded = false,
            Status = "Failed",
            SagaId = metadata.SagaId,
            OrchestrationInstanceId = metadata.OrchestrationInstanceId,
            CorrelationId = metadata.CorrelationId,
            StageKey = metadata.CurrentStage,
            TaskKey = metadata.CurrentTasks?.FirstOrDefault(),
            TaskExecutionId = metadata.TaskExecutionId,
            DispatchId = metadata.DispatchId,
            Attempt = metadata.Attempt,
            RequestType = requestType?.FullName,
            ExecutionTimeMs = executionTime.HasValue ? Convert.ToInt64(executionTime.Value.TotalMilliseconds) : null,
            Error = new RuntimeTaskResponseError
            {
                Code = exception?.GetType().Name,
                Message = exception?.Message ?? reason,
                ExceptionType = exception?.GetType().FullName,
                Reason = reason
            }
        };
    }
}
