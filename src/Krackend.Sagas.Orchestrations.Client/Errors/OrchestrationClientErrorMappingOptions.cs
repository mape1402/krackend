namespace Krackend.Sagas.Orchestrations.Client.Errors;

/// <summary>
/// Configures how client exceptions are translated to orchestration error codes.
/// </summary>
public sealed class OrchestrationClientErrorMappingOptions
{
    private readonly List<OrchestrationClientExceptionErrorMapping> _mappings = new();

    /// <summary>
    /// Gets or sets the error code used when no exception mapping matches.
    /// </summary>
    public string DefaultErrorCode { get; set; } = "UnhandledException";

    /// <summary>
    /// Gets the registered exception mappings.
    /// </summary>
    public IReadOnlyList<OrchestrationClientExceptionErrorMapping> Mappings => _mappings;

    /// <summary>
    /// Maps an exception type to an orchestration error code.
    /// </summary>
    /// <typeparam name="TException">Exception type matched by the mapping.</typeparam>
    /// <param name="errorCode">Error code understood by the orchestrator definition.</param>
    public void Map<TException>(string errorCode)
        where TException : Exception
        => Map(typeof(TException), errorCode);

    /// <summary>
    /// Maps an exception type to an orchestration error code.
    /// </summary>
    /// <param name="exceptionType">Exception type matched by the mapping.</param>
    /// <param name="errorCode">Error code understood by the orchestrator definition.</param>
    public void Map(Type exceptionType, string errorCode)
    {
        if (exceptionType is null)
        {
            throw new ArgumentNullException(nameof(exceptionType));
        }

        if (!typeof(Exception).IsAssignableFrom(exceptionType))
        {
            throw new ArgumentException("Mapped type must derive from Exception.", nameof(exceptionType));
        }

        var existingIndex = _mappings.FindIndex(mapping => mapping.ExceptionType == exceptionType);
        if (existingIndex >= 0)
        {
            _mappings.RemoveAt(existingIndex);
        }

        _mappings.Add(new OrchestrationClientExceptionErrorMapping(exceptionType, errorCode));
    }
}
