namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control
{
    /// <summary>
    /// Represents a runtime decision produced by the orchestration decision control.
    /// </summary>
    public interface IDecision
    {
        /// <summary>
        /// Gets the stable decision kind handled by the runtime.
        /// </summary>
        string Kind { get; }
    }
}
