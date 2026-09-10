namespace Krackend.Sagas.Orchestrations.Client.Publishing;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Errors;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;

internal sealed class DefaultOrchestrationExecutionResultMetadataFactory : IOrchestrationExecutionResultMetadataFactory
{
    private readonly IOrchestrationOperationExecutionContext _executionContext;
    private readonly IOrchestrationExceptionErrorCodeMapper _errorCodeMapper;
    private readonly IOptions<OrchestrationClientErrorMappingOptions> _options;

    public DefaultOrchestrationExecutionResultMetadataFactory(
        IOrchestrationOperationExecutionContext executionContext,
        IOrchestrationExceptionErrorCodeMapper errorCodeMapper,
        IOptions<OrchestrationClientErrorMappingOptions> options)
    {
        _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        _errorCodeMapper = errorCodeMapper ?? throw new ArgumentNullException(nameof(errorCodeMapper));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public OrchestrationExecutionResultMetadata CreateSuccess(
        Type requestType,
        Type responseType,
        OrchestrationOperationOptions options)
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
            ResponseType = responseType?.FullName,
            ServiceName = options?.ServiceName,
            OperationName = string.IsNullOrWhiteSpace(options?.OperationName)
                ? requestType?.FullName
                : options.OperationName,
            Metadata = CloneMetadata(options)
        };
    }

    public OrchestrationExecutionResultMetadata CreateFailure(
        Type requestType,
        Exception exception,
        OrchestrationOperationOptions options)
    {
        var completedOnUtc = DateTime.UtcNow;
        var startedOnUtc = ResolveStartedOnUtc();
        var errorResolution = _errorCodeMapper.Resolve(exception);
        var optionsValue = _options.Value;

        return new OrchestrationExecutionResultMetadata
        {
            Succeeded = false,
            Status = "Failed",
            ErrorCode = errorResolution.ErrorCode,
            ErrorMessage = ResolveErrorMessage(exception, errorResolution, optionsValue),
            ErrorType = optionsValue.IncludeExceptionDetails ? exception?.GetType().FullName : null,
            IsRetryableCandidate = errorResolution.IsRetryableCandidate,
            StartedOnUtc = startedOnUtc,
            CompletedOnUtc = completedOnUtc,
            ExecutionTimeMs = CalculateExecutionTimeMs(startedOnUtc, completedOnUtc),
            RequestType = requestType?.FullName,
            ServiceName = options?.ServiceName,
            OperationName = string.IsNullOrWhiteSpace(options?.OperationName)
                ? requestType?.FullName
                : options.OperationName,
            Metadata = CloneMetadata(options)
        };
    }

    private static string ResolveErrorMessage(
        Exception exception,
        OrchestrationExceptionErrorCodeResolution errorResolution,
        OrchestrationClientErrorMappingOptions options)
    {
        if (options.IncludeExceptionDetails)
        {
            return exception?.Message;
        }

        return string.IsNullOrWhiteSpace(options.RedactedExceptionMessage)
            ? errorResolution.ErrorCode
            : options.RedactedExceptionMessage;
    }

    private DateTime ResolveStartedOnUtc()
        => _executionContext.StartedOnUtc == default
            ? DateTime.UtcNow
            : _executionContext.StartedOnUtc;

    private long CalculateExecutionTimeMs(DateTime startedOnUtc, DateTime completedOnUtc)
        => Math.Max(0, (long)(completedOnUtc - startedOnUtc).TotalMilliseconds);

    private static Dictionary<string, JsonNode> CloneMetadata(OrchestrationOperationOptions options)
    {
        if (options?.Metadata is null || options.Metadata.Count == 0)
        {
            return new Dictionary<string, JsonNode>();
        }

        return options.Metadata.ToDictionary(
            x => x.Key,
            x => x.Value?.DeepClone(),
            StringComparer.Ordinal);
    }
}
