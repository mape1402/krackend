namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Protects outbound Runtime node secrets stored by the control plane.
/// </summary>
public interface IControlPlaneRuntimeNodeSecretProtector
{
    /// <summary>
    /// Protects a plain secret for storage.
    /// </summary>
    /// <param name="secret">Plain secret.</param>
    /// <returns>Protected secret.</returns>
    string Protect(string secret);

    /// <summary>
    /// Restores a protected secret.
    /// </summary>
    /// <param name="protectedSecret">Protected secret.</param>
    /// <returns>Plain secret.</returns>
    string Unprotect(string protectedSecret);
}
