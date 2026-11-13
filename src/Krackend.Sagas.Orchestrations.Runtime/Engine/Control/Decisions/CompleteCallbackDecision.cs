using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal sealed record CompleteCallbackDecision(
        Id InstanceId,
        Id TaskExecutionId,
        Id DispatchId,
        string Payload,
        OrchestrationExecutionResultMetadata ExecutionResultMetadata) : IDecision
    {
        public string Kind => "complete-callback";

        // The result was already committed before an interruption. Re-evaluate the saga
        // without applying the callback or its payload a second time.
        public bool ResumeOnly { get; init; }
    }
}
