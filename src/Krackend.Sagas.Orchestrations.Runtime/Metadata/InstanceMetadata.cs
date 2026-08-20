namespace Krackend.Sagas.Orchestrations.Runtime.Metadata
{
    public class InstanceMetadata
    {
        public string SagaId { get; set; }

        public string OrchestrationInstanceId { get; set; }

        public string CurrentStage { get; set; }

        public string[] CurrentTasks { get; set; }

        public string CorrelationId { get; set; }

        public string TaskExecutionId { get; set; }

        public string DispatchId { get; set; }

        public int Attempt { get; set; }

        public string BackchannelTopic { get; set; }

        public string BackchannelVersion { get; set; }
    }
}
