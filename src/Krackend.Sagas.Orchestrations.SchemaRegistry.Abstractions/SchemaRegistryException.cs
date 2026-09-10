namespace Krackend.Sagas.Orchestrations.SchemaRegistry;

/// <summary>
/// Represents an error raised while resolving schema contracts.
/// </summary>
public sealed class SchemaRegistryException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaRegistryException"/> class.
    /// </summary>
    public SchemaRegistryException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaRegistryException"/> class.
    /// </summary>
    public SchemaRegistryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
