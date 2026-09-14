namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

/// <summary>
/// Carries orchestration correlation data through transports without changing the business payload.
/// </summary>
public class OrchestrationMessageMetadata
{
    /// <summary>
    /// Gets or sets the business saga id.
    /// </summary>
    public string SagaId { get; set; }

    /// <summary>
    /// Gets or sets the orchestration instance id.
    /// </summary>
    public string OrchestrationInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the current stage key.
    /// </summary>
    public string CurrentStage { get; set; }

    /// <summary>
    /// Gets or sets the current task keys.
    /// </summary>
    public string[] CurrentTasks { get; set; }

    /// <summary>
    /// Gets or sets the correlation id.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the task execution id.
    /// </summary>
    public string TaskExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the dispatch id.
    /// </summary>
    public string DispatchId { get; set; }

    /// <summary>
    /// Gets or sets the execution attempt number.
    /// </summary>
    public int Attempt { get; set; }

    /// <summary>
    /// Gets or sets the address where orchestrated services must send their response.
    /// </summary>
    public OrchestrationReplyAddress ReplyAddress { get; set; }
}
