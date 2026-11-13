namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

/// <summary>Tracks bounded progress through timeout repair pages within one runtime replica.</summary>
internal sealed class OrchestrationTimeoutRepairCursor
{
    public OrchestrationTimeoutRepairCursor() { }
    internal SemaphoreSlim Gate { get; } = new(1, 1);
    internal int DueOffset { get; set; }
    internal int MissingActionOffset { get; set; }
    internal bool WarnedMissingPaging { get; set; }
}
