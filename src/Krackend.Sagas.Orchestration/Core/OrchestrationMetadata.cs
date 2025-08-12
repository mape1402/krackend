namespace Krackend.Sagas.Orchestration.Core
{
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Contains metadata information for a saga orchestration message, including identifiers, stages, and message context.
    /// </summary>
    public class OrchestrationMetadata
    {
        private OperationalResults _operationResults = new();

        /// <summary>
        /// Represents the key used for accessing orchestration metadata in the Krackend Sagas framework.
        /// </summary>
        /// <remarks>This constant is used to identify and retrieve metadata associated with orchestration
        /// processes. It is intended for use within the Krackend Sagas framework to ensure consistent access to
        /// metadata across different components.</remarks>
        public const string MetadataKey = "Krackend.Sagas.Orchestration.Metadata";

        /// <summary>
        /// Gets or sets the unique key for the orchestration instance.
        /// </summary>
        public string OrchestrationKey { get; init; }

        /// <summary>
        /// Gets or sets the name of the orchestration.
        /// </summary>
        public string OrchestrationName { get; init; }

        /// <summary>
        /// Gets or sets the name of the previous stage in the orchestration.
        /// </summary>
        public string PreviousStage { get; init; }

        /// <summary>
        /// Gets or sets the name of the current stage in the orchestration.
        /// </summary>
        public string CurrentStage { get; init; }

        /// <summary>
        /// Gets or sets the name of the next stage in the orchestration.
        /// </summary>
        public string NextStage { get; init; }

        /// <summary>
        /// Gets or sets the unique identifier for the message.
        /// </summary>
        public string MessageId { get; init; }

        /// <summary>
        /// Gets or sets the type of the message (event, command, or compensation command).
        /// </summary>
        public MessageType MessageType { get; init; }

        /// <summary>
        /// Gets or sets the unique identifier for the saga instance.
        /// </summary>
        public string SagaId { get; init; }

        /// <summary>
        /// Gets or sets the correlation identifier for tracking related messages.
        /// </summary>
        public string CorrelationId { get; init; }

        /// <summary>
        /// Gets or sets the source of the correlation identifier.
        /// </summary>
        public string CorrelationSource { get; init; }

        /// <summary>
        /// Gets or sets the identifier for the cause of the message.
        /// </summary>
        public string CausationId { get; init; }

        /// <summary>
        /// Gets or sets the timestamp when the message was created or processed.
        /// </summary>
        public DateTimeOffset Timestamp { get; init; }

        /// <summary>
        /// Gets or sets the number of times the message has been retried.
        /// </summary>
        public int RetryCount { get; init; }

        /// <summary>
        /// Gets or sets the maximum number of retries allowed for the message.
        /// </summary>
        public int MaxRetries { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether the message is a compensation action.
        /// </summary>
        public bool IsCompensation { get; init; }

        /// <summary>
        /// Gets or sets the topic to which replies should be sent.
        /// </summary>
        public string OrchestrationTopic { get; init; }

        /// <summary>
        /// Gets or sets the version of the orchestration topic.
        /// </summary>
        public SemanticVersion OrchestrationTopicVersion { get; init; }

        /// <summary>
        /// Gets the results of the operation, including any relevant data and status information.
        /// </summary>
        public OperationalResults OperationalResults
        {
            get => _operationResults;
            init => _operationResults = value ?? new();
        }
    }
}
