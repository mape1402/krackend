namespace Krackend.Sagas.Orchestrations.Web;

public sealed class RuntimeTriggerResult
{
    public bool Accepted { get; set; }
    public string BufferItemId { get; set; }
    public string Status { get; set; }
    public string Message { get; set; }
}
