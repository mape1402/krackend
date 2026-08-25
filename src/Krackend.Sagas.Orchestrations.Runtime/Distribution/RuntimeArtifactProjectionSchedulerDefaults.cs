namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Provides runtime artifact projection scheduler defaults.
/// </summary>
public static class RuntimeArtifactProjectionSchedulerDefaults
{
    /// <summary>
    /// Gets the Mule lane used for artifact ingress projection.
    /// </summary>
    public const string Lane = "runtime-artifacts";
}
