namespace Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;

public enum RuntimeNodeStatus
{
    Active = 1,
    Disabled = 2,
    Revoked = 3
}

public enum DistributionMode
{
    Push = 1,
    Pull = 2,
    Hybrid = 3
}

public enum RuntimeAuthenticationMode
{
    None = 1,
    ClientCredentials = 2,
    ApiKey = 3
}

public enum ReleaseTargetStatus
{
    Pending = 1,
    AvailableForPull = 2,
    PushScheduled = 3,
    InProgress = 4,
    Delivered = 5,
    Acknowledged = 6,
    Activated = 7,
    Failed = 8,
    Cancelled = 9
}

public enum ReleaseStatus
{
    Draft = 1,
    InProgress = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}

public enum ActivationStatus
{
    NotActivated = 1,
    Activating = 2,
    Activated = 3,
    ActivationFailed = 4
}

