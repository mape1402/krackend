namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Defines Mule action names used by runtime artifact lifecycle operations.
/// </summary>
public static class RuntimeArtifactActionNames
{
    /// <summary>
    /// Durable action that projects ingress configuration from an accepted runtime artifact.
    /// </summary>
    public const string ProjectIngressConfigurations = "ProjectIngressConfigurations";

    /// <summary>
    /// Durable action that stands up ready ingress configuration in one runtime replica.
    /// </summary>
    public const string StandUpArtifactIngress = "StandUpArtifactIngress";
}
