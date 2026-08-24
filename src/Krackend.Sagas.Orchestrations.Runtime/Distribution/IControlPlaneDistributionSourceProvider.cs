namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Provides control-plane sources registered for a runtime node.
/// </summary>
public interface IControlPlaneDistributionSourceProvider
{
    /// <summary>
    /// Returns every enabled control-plane source.
    /// </summary>
    IReadOnlyCollection<ControlPlaneDistributionSource> GetAll();

    /// <summary>
    /// Returns a source by its local runtime key.
    /// </summary>
    ControlPlaneDistributionSource GetByKey(string sourceKey);

    /// <summary>
    /// Returns a source by its signing key identifier.
    /// </summary>
    ControlPlaneDistributionSource GetByClientId(string clientId);
}
