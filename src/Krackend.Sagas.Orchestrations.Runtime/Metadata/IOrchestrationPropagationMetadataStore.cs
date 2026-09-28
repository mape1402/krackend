using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

namespace Krackend.Sagas.Orchestrations.Runtime.Metadata
{
    internal interface IOrchestrationPropagationMetadataStore
    {
        OrchestrationPropagationMetadata Load(OrchestrationInstance instance);

        void Save(OrchestrationInstance instance, OrchestrationPropagationMetadata metadata);
    }
}
