namespace Krackend.Sagas.Orchestrations.Client.Publishing;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

internal sealed class DefaultOrchestrationExecutionResultMetadataFactory : IOrchestrationExecutionResultMetadataFactory
{
    private readonly IOrchestrationOperationExecutionContext _executionContext;

    public DefaultOrchestrationExecutionResultMetadataFactory(IOrchestrationOperationExecutionContext executionContext)
    {
        _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
    }

    public OrchestrationExecutionResultMetadata CreateSuccess(Type requestType, Type responseType)
    {
        var completedOnUtc = DateTime.UtcNow;
        var startedOnUtc = ResolveStartedOnUtc();

        return new OrchestrationExecutionResultMetadata
        {
            Succeeded = true,
            Status = "Succeeded",
            StartedOnUtc = startedOnUtc,
            CompletedOnUtc = completedOnUtc,
            ExecutionTimeMs = CalculateExecutionTimeMs(startedOnUtc, completedOnUtc),
            RequestType = requestType?.FullName,
            ResponseType = responseType?.FullName
        };
    }

    public OrchestrationExecutionResultMetadata CreateFailure(Type requestType, Exception exception)
    {
        var completedOnUtc = DateTime.UtcNow;
        var startedOnUtc = ResolveStartedOnUtc();

        return new OrchestrationExecutionResultMetadata
        {
            Succeeded = false,
            Status = "Failed",
            ErrorCode = exception?.GetType().Name,
            ErrorMessage = exception?.Message,
            ErrorType = exception?.GetType().FullName,
            StartedOnUtc = startedOnUtc,
            CompletedOnUtc = completedOnUtc,
            ExecutionTimeMs = CalculateExecutionTimeMs(startedOnUtc, completedOnUtc),
            RequestType = requestType?.FullName
        };
    }

    private DateTime ResolveStartedOnUtc()
        => _executionContext.StartedOnUtc == default
            ? DateTime.UtcNow
            : _executionContext.StartedOnUtc;

    private long CalculateExecutionTimeMs(DateTime startedOnUtc, DateTime completedOnUtc)
        => Math.Max(0, (long)(completedOnUtc - startedOnUtc).TotalMilliseconds);
}
