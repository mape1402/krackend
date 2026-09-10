namespace Krackend.Sagas.Orchestrations.Client.Errors;

using Microsoft.Extensions.Options;

internal sealed class DefaultOrchestrationExceptionErrorCodeMapper : IOrchestrationExceptionErrorCodeMapper
{
    private readonly IOptions<OrchestrationClientErrorMappingOptions> _options;

    public DefaultOrchestrationExceptionErrorCodeMapper(IOptions<OrchestrationClientErrorMappingOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public OrchestrationExceptionErrorCodeResolution Resolve(Exception exception)
    {
        var configured = _options.Value;
        if (exception is null)
        {
            return ResolveDefaultErrorCode(configured, null);
        }

        var exceptionType = exception.GetType();
        var mapping = configured.Mappings
            .Where(candidate => candidate.Matches(exception))
            .OrderBy(candidate => GetInheritanceDistance(exceptionType, candidate.ExceptionType))
            .FirstOrDefault();

        return mapping is null
            ? ResolveDefaultErrorCode(configured, exception)
            : new OrchestrationExceptionErrorCodeResolution(
                mapping.ErrorCode,
                mapping.IsRetryableCandidate);
    }

    private OrchestrationExceptionErrorCodeResolution ResolveDefaultErrorCode(
        OrchestrationClientErrorMappingOptions options,
        Exception exception)
    {
        if (!string.IsNullOrWhiteSpace(options.DefaultErrorCode))
        {
            return new OrchestrationExceptionErrorCodeResolution(options.DefaultErrorCode);
        }

        return new OrchestrationExceptionErrorCodeResolution(exception?.GetType().Name ?? "UnhandledException");
    }

    private static int GetInheritanceDistance(Type exceptionType, Type mappedType)
    {
        var distance = 0;
        var current = exceptionType;

        while (current is not null)
        {
            if (current == mappedType)
            {
                return distance;
            }

            distance++;
            current = current.BaseType;
        }

        return int.MaxValue;
    }
}
