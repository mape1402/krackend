using Krackend.EventSourcing.Metadata;

namespace Krackend.EventSourcing.Configuration;

/// <summary>
/// Configures event envelope metadata.
/// </summary>
public sealed class EventEnvelopeOptions
{
    private readonly List<IEventMetadataProvider> _metadataProviders = [];

    /// <summary>
    /// Gets configured dynamic metadata providers.
    /// </summary>
    public IReadOnlyCollection<IEventMetadataProvider> MetadataProviders => _metadataProviders;

    /// <summary>
    /// Adds a dynamic metadata provider.
    /// </summary>
    public EventEnvelopeOptions AddMetadata(string key, Func<IServiceProvider, object?> valueFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(valueFactory);

        _metadataProviders.Add(new DelegateEventMetadataProvider(key, valueFactory));
        return this;
    }

    /// <summary>
    /// Adds a dynamic metadata provider.
    /// </summary>
    public EventEnvelopeOptions AddMetadata(IEventMetadataProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _metadataProviders.Add(provider);
        return this;
    }
}
