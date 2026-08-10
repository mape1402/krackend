namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Result returned after attempting to materialize an artifact in Runtime.
/// </summary>
public sealed class RuntimeArtifactDeploymentResult
{
    public bool Accepted { get; set; }
    public string RuntimeArtifactId { get; set; }
    public string EnvironmentKey { get; set; }
    public string Status { get; set; }
    public string Message { get; set; }
    public string CorrelationId { get; set; }

    public static RuntimeArtifactDeploymentResult Accept(
        string runtimeArtifactId,
        string environmentKey,
        string status,
        string message,
        string correlationId)
        => new()
        {
            Accepted = true,
            RuntimeArtifactId = runtimeArtifactId,
            EnvironmentKey = environmentKey,
            Status = status,
            Message = message,
            CorrelationId = correlationId
        };

    public static RuntimeArtifactDeploymentResult Reject(
        string environmentKey,
        string message,
        string correlationId)
        => new()
        {
            Accepted = false,
            EnvironmentKey = environmentKey,
            Status = "Rejected",
            Message = message,
            CorrelationId = correlationId
        };
}
