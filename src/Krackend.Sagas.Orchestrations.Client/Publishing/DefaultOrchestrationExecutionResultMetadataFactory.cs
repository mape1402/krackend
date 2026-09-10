namespace Krackend.Sagas.Orchestrations.Client.Publishing;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Errors;

internal sealed class DefaultOrchestrationExecutionResultMetadataFactory : IOrchestrationExecutionResultMetadataFactory
{
    private readonly IOrchestrationOperationExecutionContext _executionContext;
    private readonly IOrchestrationExceptionErrorCodeMapper _errorCodeMapper;

    public DefaultOrchestrationExecutionResultMetadataFactory(
        IOrchestrationOperationExecutionContext executionContext,
        IOrchestrationExceptionErrorCodeMapper errorCodeMapper)
    {
        _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        _errorCodeMapper = errorCodeMapper ?? throw new ArgumentNullException(nameof(errorCodeMapper));
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
            ErrorCode = _errorCodeMapper.Resolve(exception),
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
