namespace Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;

using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

internal sealed class RealMessagingScenario
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<RealMessagingServiceOutcome>> _outcomes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<RealMessagingServiceInvocation> _invocations = new();

    public IReadOnlyCollection<RealMessagingServiceInvocation> Invocations => _invocations.ToArray();

    public void Enqueue(string topic, RealMessagingServiceOutcome outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(outcome);

        _outcomes.GetOrAdd(topic, _ => new ConcurrentQueue<RealMessagingServiceOutcome>()).Enqueue(outcome);
    }

    public void Record(string topic, JsonNode? payload, OrchestrationMessageMetadata metadata)
    {
        _invocations.Enqueue(new RealMessagingServiceInvocation(
            topic,
            payload?.DeepClone(),
            CloneMetadata(metadata)));
    }

    public RealMessagingServiceOutcome NextOutcome(string topic)
    {
        if (_outcomes.TryGetValue(topic, out var outcomes) &&
            outcomes.TryDequeue(out var outcome))
        {
            return outcome;
        }

        return RealMessagingServiceOutcome.Success(new JsonObject
        {
            ["topic"] = topic,
            ["handled"] = true
        });
    }

    public int CountInvocations(string topic)
        => _invocations.Count(invocation => string.Equals(invocation.Topic, topic, StringComparison.OrdinalIgnoreCase));

    private static OrchestrationMessageMetadata CloneMetadata(OrchestrationMessageMetadata metadata)
        => new()
        {
            SagaId = metadata.SagaId,
            OrchestrationInstanceId = metadata.OrchestrationInstanceId,
            CurrentStage = metadata.CurrentStage,
            CurrentTasks = metadata.CurrentTasks?.ToArray(),
            CorrelationId = metadata.CorrelationId,
            TaskExecutionId = metadata.TaskExecutionId,
            DispatchId = metadata.DispatchId,
            Attempt = metadata.Attempt,
            ReplyAddress = CloneReplyAddress(metadata.ReplyAddress)
        };

    private static OrchestrationReplyAddress? CloneReplyAddress(OrchestrationReplyAddress? address)
        => address is null
            ? null
            : new OrchestrationReplyAddress
            {
                Transport = address.Transport,
                SettingsPayload = address.SettingsPayload,
                Metadata = address.Metadata.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value?.DeepClone(),
                    StringComparer.Ordinal)
            };
}
