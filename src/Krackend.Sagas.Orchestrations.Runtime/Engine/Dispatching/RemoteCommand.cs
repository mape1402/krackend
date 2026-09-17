namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching
{
    /// <summary>
    /// Describes a remote command dispatch requested by the orchestration engine.
    /// </summary>
    public class RemoteCommand
    {
        /// <summary>
        /// Gets or sets the transport that must execute the command.
        /// </summary>
        public RemoteCommandTransport RemoteCommandTransport { get; set; }

        /// <summary>
        /// Gets or sets the business payload to send to the remote service.
        /// </summary>
        public string Payload { get; set; }

        /// <summary>
        /// Gets or sets the transport-specific command settings.
        /// </summary>
        public string SettingsPayload { get; set; }

        /// <summary>
        /// Gets or sets the orchestration instance id associated with this dispatch.
        /// </summary>
        public string OrchestrationInstanceId { get; set; }

        /// <summary>
        /// Gets or sets the stage execution id associated with this dispatch.
        /// </summary>
        public string StageExecutionId { get; set; }

        /// <summary>
        /// Gets or sets the task execution id associated with this dispatch.
        /// </summary>
        public string TaskExecutionId { get; set; }

        /// <summary>
        /// Gets or sets the task execution attempt id associated with this dispatch.
        /// </summary>
        public string TaskExecutionAttemptId { get; set; }

        /// <summary>
        /// Gets or sets the dispatch id associated with this command.
        /// </summary>
        public string DispatchId { get; set; }

        /// <summary>
        /// Gets or sets the stage key associated with this dispatch.
        /// </summary>
        public string StageKey { get; set; }

        /// <summary>
        /// Gets or sets the task key associated with this dispatch.
        /// </summary>
        public string TaskKey { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the remote command expects a callback.
        /// </summary>
        public bool AwaitResponse { get; set; }

        /// <summary>
        /// Gets or sets the UTC instant when the transport command can be executed.
        /// </summary>
        public DateTimeOffset? ScheduledOnUtc { get; set; }

        /// <summary>
        /// Gets or sets the orchestration message metadata that must be attached by the transport adapter.
        /// </summary>
        public Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata.OrchestrationMessageMetadata MessageMetadata { get; set; }
    }
}
