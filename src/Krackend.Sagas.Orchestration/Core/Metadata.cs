﻿namespace Krackend.Sagas.Orchestration.Core
{
    /// <summary>
    /// Contains metadata information for a saga orchestration message, including identifiers, stages, and message context.
    /// </summary>
    public class Metadata
    {
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
        public string OrchestrationKey { get; internal set; }

        /// <summary>
        /// Gets or sets the name of the orchestration.
        /// </summary>
        public string OrchestrationName { get; internal set; }

        /// <summary>
        /// Gets or sets the name of the previous stage in the orchestration.
        /// </summary>
        public string PreviousStage { get; internal set; }

        /// <summary>
        /// Gets or sets the name of the current stage in the orchestration.
        /// </summary>
        public string CurrentStage { get; internal set; }

        /// <summary>
        /// Gets or sets the name of the next stage in the orchestration.
        /// </summary>
        public string NextStage { get; internal set; }

        /// <summary>
        /// Gets or sets the unique identifier for the message.
        /// </summary>
        public string MessageId { get; internal set; }

        /// <summary>
        /// Gets or sets the type of the message (event, command, or compensation command).
        /// </summary>
        public MessageType MessageType { get; internal set; }

        /// <summary>
        /// Gets or sets the unique identifier for the saga instance.
        /// </summary>
        public string SagaId { get; internal set; }

        /// <summary>
        /// Gets or sets the correlation identifier for tracking related messages.
        /// </summary>
        public string CorrelationId { get; internal set; }

        /// <summary>
        /// Gets or sets the source of the correlation identifier.
        /// </summary>
        public string CorrelationSource { get; internal set; }

        /// <summary>
        /// Gets or sets the identifier for the cause of the message.
        /// </summary>
        public string CausationId { get; internal set; }

        /// <summary>
        /// Gets or sets the timestamp when the message was created or processed.
        /// </summary>
        public DateTimeOffset Timestamp { get; internal set; }

        /// <summary>
        /// Gets or sets the number of times the message has been retried.
        /// </summary>
        public int RetryCount { get; internal set; }

        /// <summary>
        /// Gets or sets the maximum number of retries allowed for the message.
        /// </summary>
        public int MaxRetries { get; internal set; }

        /// <summary>
        /// Gets or sets a value indicating whether the message is a compensation action.
        /// </summary>
        public bool IsCompensation { get; internal set; }

        /// <summary>
        /// Gets or sets the queue to which replies should be sent.
        /// </summary>
        public string ReplyTo { get; internal set; }

        /// <summary>
        /// Gets the results of the operation, including any relevant data and status information.
        /// </summary>
        public OperationalResults OperationalResults { get; internal set; }
    }
}
