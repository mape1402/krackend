namespace Krackend.Sagas.Orchestrations.Web;

public sealed class RuntimeTriggerRequest
{
    public string TriggerType { get; set; } = "Event";
    public string TriggerKey { get; set; }
    public string EnvironmentKey { get; set; }
    public string CorrelationId { get; set; }
    public string IdempotencyKey { get; set; }
    public string SourceMessageId { get; set; }
    public string PayloadJson { get; set; }
}
