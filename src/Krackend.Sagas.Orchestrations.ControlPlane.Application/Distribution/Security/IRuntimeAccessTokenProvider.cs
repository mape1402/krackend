using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Provides cached access tokens for outbound Design calls into Runtime.
/// </summary>
public interface IRuntimeAccessTokenProvider
{
    /// <summary>
    /// Attaches a bearer token to an outbound request.
    /// </summary>
    /// <param name="request">HTTP request to authenticate.</param>
    /// <param name="node">Runtime node configuration.</param>
    /// <param name="scopes">Required scopes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AttachTokenAsync(
        HttpRequestMessage request,
        RuntimeNode node,
        IReadOnlyCollection<ArtifactDeliveryScope> scopes,
        CancellationToken cancellationToken = default);
}
