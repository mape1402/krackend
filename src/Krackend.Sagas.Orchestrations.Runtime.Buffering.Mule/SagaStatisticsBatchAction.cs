using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

/// <summary>A durable scheduled statistics projection batch.</summary>
[MuleAction(MuleActionKeys.StatisticsBatchAction)]
public sealed class SagaStatisticsBatchAction(IServiceProvider services, IOptions<SagaStatisticsMuleOptions> options,
    ILogger<SagaStatisticsBatchAction> logger)
    : IMuleAction<SagaStatisticsBatchRequest>
{
    /// <inheritdoc />
    public async ValueTask ExecuteAsync(MuleActionContext<SagaStatisticsBatchRequest> context, CancellationToken cancellationToken)
    {
        var size = Math.Clamp(options.Value.BatchSize, 1, 1000);
        var backfill = services.GetService<ISagaStatisticsBackfill>();
        if (backfill is not null) await backfill.ProcessAsync(size, cancellationToken);
        var processor = services.GetService<ISagaStatisticsBatchProcessor>();
        if (processor is not null)
        {
            var result = await processor.ProcessAsync(size, cancellationToken);
            var publisher = services.GetService<IRuntimeStatisticsUpdatePublisher>();
            if (result.Applied > 0)
            {
                if (publisher is not null)
                {
                    try { await publisher.PublishAsync(CancellationToken.None); }
                    catch (Exception exception) { logger.LogWarning(exception, "Could not notify local statistics clients."); }
                }
                var fanout = services.GetService<IRuntimeStatisticsFanout>();
                if (fanout is not null)
                {
                    try { await fanout.PublishStatisticsUpdatedAsync(CancellationToken.None); }
                    catch (Exception exception) { logger.LogWarning(exception, "Could not notify remote statistics clients."); }
                }
            }
        }
    }
}

/// <summary>Identifies a scheduled statistics batch.</summary>
public sealed class SagaStatisticsBatchRequest
{
    /// <summary>Gets or sets the scheduled time window.</summary>
    public long Window { get; set; }
}
