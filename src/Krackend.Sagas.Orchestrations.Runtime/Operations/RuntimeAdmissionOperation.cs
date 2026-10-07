namespace Krackend.Sagas.Orchestrations.Runtime.Operations;

/// <summary>
/// Identifies a runtime operation that must pass admission before it can mutate or accept orchestration work.
/// </summary>
public enum RuntimeAdmissionOperation
{
    /// <summary>
    /// A trigger or event is being admitted into the runtime.
    /// </summary>
    TriggerIntake,

    /// <summary>
    /// A backchannel reply or callback is being admitted into the runtime.
    /// </summary>
    BackchannelIntake,

    /// <summary>
    /// The engine is about to evaluate or advance orchestration state.
    /// </summary>
    OrchestrationExecution,

    /// <summary>
    /// A command dispatch is about to be published to a transport.
    /// </summary>
    Dispatch,

    /// <summary>
    /// Timeout processing is about to inspect or mutate waiting tasks.
    /// </summary>
    TimeoutProcessing,

    /// <summary>
    /// Operator-driven recovery is about to mutate an orchestration instance.
    /// </summary>
    Recovery,

    /// <summary>
    /// Automated runtime reconciliation is about to inspect or repair durable state.
    /// </summary>
    Reconciliation,

    /// <summary>
    /// Runtime ingress is about to stand up or reconnect.
    /// </summary>
    IngressStandup,

    /// <summary>
    /// Artifact projection is about to write runtime ingress/configuration state.
    /// </summary>
    ArtifactProjection
}
