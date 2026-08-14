namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;

/// <summary>
/// Identifies the kind of input accepted by the runtime ingress.
/// </summary>
public enum RuntimeIngressKind
{
    /// <summary>
    /// Input kind was not specified.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Input starts a new orchestration instance.
    /// </summary>
    Trigger = 1,

    /// <summary>
    /// Input continues a task waiting for a response.
    /// </summary>
    TaskResponse = 2
}
