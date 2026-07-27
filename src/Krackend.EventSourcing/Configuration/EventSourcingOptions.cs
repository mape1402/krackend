using Krackend.EventSourcing.Metadata;
using System.Reflection;

namespace Krackend.EventSourcing.Configuration;

/// <summary>
/// Configures Krackend event sourcing services.
/// </summary>
public sealed class EventSourcingOptions
{
    /// <summary>
    /// Gets envelope configuration.
    /// </summary>
    public EventEnvelopeOptions Envelope { get; } = new();

    /// <summary>
    /// Gets logical event store configuration.
    /// </summary>
    public EventStoreOptionsCollection Stores { get; } = new();

    /// <summary>
    /// Gets event routing configuration.
    /// </summary>
    public EventRoutingOptions Routing { get; } = new();

    /// <summary>
    /// Gets or sets the number of stream events read per rehydration batch.
    /// </summary>
    public int RehydrationBatchSize { get; set; } = 500;

    /// <summary>
    /// Gets assemblies scanned for event sourcing components.
    /// </summary>
    public List<Assembly> Assemblies { get; } = [];

    /// <summary>
    /// Adds an assembly to component discovery.
    /// </summary>
    public EventSourcingOptions ScanAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        if (!Assemblies.Contains(assembly))
            Assemblies.Add(assembly);

        return this;
    }

    /// <summary>
    /// Adds the assembly that contains the specified type to component discovery.
    /// </summary>
    public EventSourcingOptions ScanAssemblyContaining<T>()
        => ScanAssembly(typeof(T).Assembly);
}
