namespace Krackend.Sagas.Orchestrations.Runtime.Metadata
{
    public class InstanceMetadata
    {
        public string SagaId { get; set; }

        public string CurrentStage { get; set; }

        public string[] CurrentTasks { get; set; }

        public string CorrelationId { get; set; }

        // mmmm should add a response data such as attempts, error info?... when a service responses to orchestrator could include this data for engine take decisions.
    }
}
