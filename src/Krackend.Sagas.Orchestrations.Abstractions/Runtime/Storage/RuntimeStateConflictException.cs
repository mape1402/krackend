namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>A concurrent durable decision won; reload state before continuing this action.</summary>
public sealed class RuntimeStateConflictException : Exception
{
    /// <summary>Creates a retryable state conflict.</summary>
    public RuntimeStateConflictException(string message, Exception innerException = null) : base(message, innerException) { }
}
