using System.Net.Http;

namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Signs outbound artifact delivery HTTP requests.
/// </summary>
public interface IArtifactDeliveryHttpRequestSigner
{
    /// <summary>
    /// Adds authentication headers to the HTTP request.
    /// </summary>
    Task SignAsync(
        HttpRequestMessage request,
        string body,
        string keyId,
        string secretReference,
        CancellationToken cancellationToken = default);
}
