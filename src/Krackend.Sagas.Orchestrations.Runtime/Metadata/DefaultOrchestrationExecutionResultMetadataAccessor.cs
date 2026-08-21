using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

namespace Krackend.Sagas.Orchestrations.Runtime.Metadata
{
    internal sealed class DefaultOrchestrationExecutionResultMetadataAccessor :
        IOrchestrationExecutionResultMetadataAccessor,
        IOrchestrationExecutionResultMetadataSetter
    {
        private OrchestrationExecutionResultMetadata _metadata;

        public OrchestrationExecutionResultMetadata Get()
            => _metadata;

        public void Set(OrchestrationExecutionResultMetadata metadata)
            => _metadata = metadata;

        public void Clear()
            => _metadata = null;
    }
}
