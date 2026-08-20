using Microsoft.Extensions.Configuration;

namespace KrackendStressDemo;

internal sealed class BatchPublisherOptions
{
    private static readonly string[] DefaultQueues =
    [
        "orchestrations.sales.sale.created",
        "events.sales.sale.created"
    ];

    public string RabbitMqConnectionString { get; init; } = "amqp://guest:guest@localhost:5672";

    public string Domain { get; init; } = "Krackend.Stress.Agent";

    public string Version { get; init; } = "1.0.0";

    public int MessagesPerQueue { get; init; } = 1_000;

    public int BatchSize { get; init; } = 100;

    public int Parallelism { get; init; } = Environment.ProcessorCount;

    public TimeSpan DelayBetweenBatches { get; init; } = TimeSpan.Zero;

    public int PayloadBytes { get; init; }

    public IReadOnlyList<string> Queues { get; init; } = DefaultQueues;

    public static BatchPublisherOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("BatchPublisher");
        var queues = ReadQueues(configuration, section);

        return new BatchPublisherOptions
        {
            RabbitMqConnectionString =
                configuration.GetConnectionString("RabbitMq")
                ?? configuration["rabbitmq"]
                ?? configuration["rabbitmq-connection-string"]
                ?? section["RabbitMqConnectionString"]
                ?? "amqp://guest:guest@localhost:5672",
            Domain = ReadString(configuration, section, "Domain", "domain", "Krackend.Stress.Agent"),
            Version = ReadString(configuration, section, "Version", "version", "1.0.0"),
            MessagesPerQueue = ReadPositiveInt(configuration, section, "MessagesPerQueue", "messages-per-queue", 1_000),
            BatchSize = ReadPositiveInt(configuration, section, "BatchSize", "batch-size", 100),
            Parallelism = ReadPositiveInt(configuration, section, "Parallelism", "parallelism", Environment.ProcessorCount),
            DelayBetweenBatches = TimeSpan.FromMilliseconds(
                ReadNonNegativeInt(configuration, section, "DelayBetweenBatchesMs", "delay-between-batches-ms", 0)),
            PayloadBytes = ReadNonNegativeInt(configuration, section, "PayloadBytes", "payload-bytes", 0),
            Queues = queues.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray()
        };
    }

    private static string[] ReadQueues(IConfiguration configuration, IConfiguration section)
    {
        var queues = configuration.GetSection("queues").Get<string[]>()
            ?? section.GetSection("Queues").Get<string[]>();

        if (queues is { Length: > 0 })
        {
            return queues;
        }

        var rawQueues = configuration["queues"] ?? section["Queues"];
        return string.IsNullOrWhiteSpace(rawQueues)
            ? DefaultQueues
            : rawQueues.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static string ReadString(
        IConfiguration configuration,
        IConfiguration section,
        string key,
        string commandLineKey,
        string fallback)
        => configuration[commandLineKey] ?? section[key] ?? fallback;

    private static int ReadPositiveInt(
        IConfiguration configuration,
        IConfiguration section,
        string key,
        string commandLineKey,
        int fallback)
    {
        var value = ReadInt(configuration, section, key, commandLineKey, fallback);
        return value > 0 ? value : fallback;
    }

    private static int ReadNonNegativeInt(
        IConfiguration configuration,
        IConfiguration section,
        string key,
        string commandLineKey,
        int fallback)
    {
        var value = ReadInt(configuration, section, key, commandLineKey, fallback);
        return value >= 0 ? value : fallback;
    }

    private static int ReadInt(
        IConfiguration configuration,
        IConfiguration section,
        string key,
        string commandLineKey,
        int fallback)
        => int.TryParse(configuration[commandLineKey] ?? section[key], out var value)
            ? value
            : fallback;
}
