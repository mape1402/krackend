namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Support;

using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using System.Text.Json.Nodes;

internal sealed class SequencedTaskDispatchRequestPayloadPreparer : ITaskDispatchRequestPayloadPreparer
{
    private readonly Queue<TaskDispatchPreparationException> _failures = new();

    public int Calls { get; private set; }

    public void FailNext(string errorCode, string message)
        => _failures.Enqueue(new TaskDispatchPreparationException(errorCode, message));

    public Task<TaskDispatchRequestPayloadPreparationResult> PrepareAsync(
        TaskDispatchRequestPayloadPreparationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Calls++;

        if (_failures.Count > 0)
        {
            throw _failures.Dequeue();
        }

        return Task.FromResult(new TaskDispatchRequestPayloadPreparationResult
        {
            Payload = ResolvePayload(request)!
        });
    }

    private static JsonNode? ResolvePayload(TaskDispatchRequestPayloadPreparationRequest request)
        => string.IsNullOrWhiteSpace(request.Payload)
            ? request.Instance.SnapshotPayload?["trigger"]?["payload"]?.DeepClone()
            : JsonNode.Parse(request.Payload);
}
