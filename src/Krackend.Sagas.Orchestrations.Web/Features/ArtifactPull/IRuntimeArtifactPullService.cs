namespace Krackend.Sagas.Orchestrations.Web;

public interface IRuntimeArtifactPullService
{
    Task<RuntimeArtifactPullResult> PullPending(CancellationToken cancellationToken = default);
}
