namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Runtime pending work type names.
/// </summary>
public static class RuntimePendingWorkTypes
{
    public const string WaitingTaskTimeout = "waiting-task-timeout";
    public const string WaitingAttemptTimeout = "waiting-attempt-timeout";
    public const string PendingCompensation = "pending-compensation";
}
