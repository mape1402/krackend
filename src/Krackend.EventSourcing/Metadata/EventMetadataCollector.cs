using Krackend.EventSourcing.Configuration;

namespace Krackend.EventSourcing.Metadata;

/// <summary>
/// Collects configured metadata values for a commit operation.
/// </summary>
public sealed class EventMetadataCollector
{
    private readonly EventEnvelopeOptions _options;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventMetadataCollector"/> class.
    /// </summary>
    public EventMetadataCollector(EventEnvelopeOptions options, IServiceProvider serviceProvider)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Collects metadata from all configured providers.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Collect()
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var provider in _options.MetadataProviders)
        {
            metadata[provider.Key] = provider.GetValue(_serviceProvider);
        }

        return metadata;
    }
}
