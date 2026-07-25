namespace Krackend.EventSourcing.Metadata;

/// <summary>
/// Provides metadata through a configured delegate.
/// </summary>
public sealed class DelegateEventMetadataProvider : IEventMetadataProvider
{
    private readonly Func<IServiceProvider, object?> _valueFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateEventMetadataProvider"/> class.
    /// </summary>
    public DelegateEventMetadataProvider(string key, Func<IServiceProvider, object?> valueFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(valueFactory);

        Key = key;
        _valueFactory = valueFactory;
    }

    /// <inheritdoc />
    public string Key { get; }

    /// <inheritdoc />
    public object? GetValue(IServiceProvider serviceProvider)
        => _valueFactory(serviceProvider);
}
