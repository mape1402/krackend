namespace Krackend.Sagas.Orchestrations.Engine.DurableWork;

using Mule;

/// <summary>
/// Mule action keys used by the orchestration runtime durable work pipeline.
/// </summary>
public static class RuntimeDurableWorkActionKeys
{
    /// <summary>
    /// Processes a canonical runtime ingress envelope.
    /// </summary>
    public static readonly ActionKey ProcessIngress = ActionKey.From("krackend.runtime.process-ingress");

    /// <summary>
    /// Dispatches a canonical runtime task envelope.
    /// </summary>
    public static readonly ActionKey DispatchTask = ActionKey.From("krackend.runtime.dispatch-task");

    /// <summary>
    /// Reconciles stalled runtime work.
    /// </summary>
    public static readonly ActionKey Reconcile = ActionKey.From("krackend.runtime.reconcile");

    /// <summary>
    /// Executes pending runtime compensation work.
    /// </summary>
    public static readonly ActionKey Compensate = ActionKey.From("krackend.runtime.compensate");
}

/// <summary>
/// Mule lanes used by the orchestration runtime durable work pipeline.
/// </summary>
public static class RuntimeDurableWorkLanes
{
    /// <summary>
    /// Durable trigger ingress lane.
    /// </summary>
    public const string TriggerIngress = "krackend.runtime.ingress.trigger";

    /// <summary>
    /// Durable task response ingress lane.
    /// </summary>
    public const string ResponseIngress = "krackend.runtime.ingress.response";

    /// <summary>
    /// Durable task dispatch lane.
    /// </summary>
    public const string Dispatch = "krackend.runtime.dispatch";

    /// <summary>
    /// Durable compensation dispatch lane.
    /// </summary>
    public const string Compensation = "krackend.runtime.compensation";

    /// <summary>
    /// Durable reconciliation lane.
    /// </summary>
    public const string Reconcile = "krackend.runtime.reconcile";
}
