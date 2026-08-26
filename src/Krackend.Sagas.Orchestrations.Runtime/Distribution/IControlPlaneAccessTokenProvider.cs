using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Provides cached access tokens for outbound Runtime calls into Design.
/// </summary>
public interface IControlPlaneAccessTokenProvider
{
    /// <summary>
    /// Attaches a bearer token to an outbound request.
    /// </summary>
    /// <param name="request">HTTP request to authenticate.</param>
    /// <param name="source">Control-plane source configuration.</param>
    /// <param name="scopes">Required scopes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AttachTokenAsync(
        HttpRequestMessage request,
        ControlPlaneDistributionSource source,
        IReadOnlyCollection<ArtifactDeliveryScope> scopes,
        CancellationToken cancellationToken = default);
}
