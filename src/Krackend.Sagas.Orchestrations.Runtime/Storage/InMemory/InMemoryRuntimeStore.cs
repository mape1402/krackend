using System.Collections.Concurrent;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory
{
    internal sealed class InMemoryRuntimeStore
    {
        public ConcurrentDictionary<Id, RuntimeOrchestrationArtifact> Artifacts { get; } = new();

        public ConcurrentDictionary<Id, RuntimeDesignNode> DesignNodes { get; } = new();

        public ConcurrentDictionary<Id, RuntimeIngressConfiguration> IngressConfigurations { get; } = new();

        public ConcurrentDictionary<Id, OrchestrationInstance> Instances { get; } = new();

        public ConcurrentDictionary<Id, StageExecution> Stages { get; } = new();

        public ConcurrentDictionary<Id, TaskExecution> Tasks { get; } = new();

        public ConcurrentDictionary<Id, TaskExecutionAttempt> Attempts { get; } = new();

        public ConcurrentDictionary<Id, TaskDispatch> Dispatches { get; } = new();

        public ConcurrentDictionary<Id, CompensationExecution> Compensations { get; } = new();

        public ConcurrentBag<ExecutionTransition> Transitions { get; } = new();
    }
}
