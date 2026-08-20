using System.Diagnostics;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;

namespace KrackendStressDemo;

internal sealed class BatchPublisher
{
    private readonly BatchPublisherOptions _options;
    private readonly StressMessageFactory _messageFactory;
    private readonly IProducer _producer;

    public BatchPublisher(
        BatchPublisherOptions options,
        StressMessageFactory messageFactory,
        IProducer producer)
    {
        _options = options;
        _messageFactory = messageFactory;
        _producer = producer;
    }

    public async Task PublishAsync(CancellationToken cancellationToken = default)
    {
        var version = SemanticVersion.Parse(_options.Version);
        var totalMessages = _options.MessagesPerQueue * _options.Queues.Count;
        var stopwatch = Stopwatch.StartNew();
        var published = 0;

        Console.WriteLine($"RabbitMQ: {_options.RabbitMqConnectionString}");
        Console.WriteLine($"Queues: {string.Join(", ", _options.Queues)}");
        Console.WriteLine($"Messages per queue: {_options.MessagesPerQueue:N0}");
        Console.WriteLine($"Batch size: {_options.BatchSize:N0}");
        Console.WriteLine($"Parallelism: {_options.Parallelism:N0}");
        Console.WriteLine();

        foreach (var queue in _options.Queues)
        {
            var queuePublished = await PublishQueueAsync(queue, version, cancellationToken);
            published += queuePublished;

            Console.WriteLine($"Queue '{queue}' published {queuePublished:N0} messages.");
        }

        stopwatch.Stop();
        var rate = published / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);
        Console.WriteLine();
        Console.WriteLine($"Published {published:N0}/{totalMessages:N0} messages in {stopwatch.Elapsed} ({rate:N0} msg/s).");
    }

    private async Task<int> PublishQueueAsync(
        string queue,
        SemanticVersion version,
        CancellationToken cancellationToken)
    {
        var published = 0;

        while (published < _options.MessagesPerQueue)
        {
            var currentBatchSize = Math.Min(_options.BatchSize, _options.MessagesPerQueue - published);
            var batchStart = published;

            await Parallel.ForEachAsync(
                Enumerable.Range(0, currentBatchSize),
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = _options.Parallelism
                },
                async (offset, token) =>
                {
                    var sequence = batchStart + offset + 1;
                    var message = _messageFactory.Create(queue, sequence);

                    await _producer.PublishAsync(message, queue, version, token);
                });

            published += currentBatchSize;
            Console.WriteLine($"  {queue}: {published:N0}/{_options.MessagesPerQueue:N0}");

            if (_options.DelayBetweenBatches > TimeSpan.Zero)
            {
                await Task.Delay(_options.DelayBetweenBatches, cancellationToken);
            }
        }

        return published;
    }
}
