using Krackend.EventSourcing.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.EventSourcing.Metadata;

/// <summary>
/// Provides envelope metadata configuration for execution context values.
/// </summary>
public static class EventEnvelopeExecutionContextExtensions
{
    /// <summary>
    /// Adds standard correlation and causation metadata from the current <see cref="IEventExecutionContext"/>.
    /// </summary>
    public static EventEnvelopeOptions UseExecutionContextMetadata(this EventEnvelopeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options
            .AddMetadata(EventExecutionContextMetadataKeys.CorrelationId, sp =>
                sp.GetService<IEventExecutionContext>()?.CorrelationId)
            .AddMetadata(EventExecutionContextMetadataKeys.CausationId, sp =>
                sp.GetService<IEventExecutionContext>()?.CausationId)
            .AddMetadata(EventExecutionContextMetadataKeys.UserId, sp =>
                sp.GetService<IEventExecutionContext>()?.UserId)
            .AddMetadata(EventExecutionContextMetadataKeys.TenantId, sp =>
                sp.GetService<IEventExecutionContext>()?.TenantId)
            .AddMetadata(EventExecutionContextMetadataKeys.Source, sp =>
                sp.GetService<IEventExecutionContext>()?.Source);
    }
}
