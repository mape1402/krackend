using Microsoft.AspNetCore.DataProtection;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Data Protection implementation for control-plane outbound Runtime secrets.
/// </summary>
public sealed class DataProtectionControlPlaneRuntimeNodeSecretProtector : IControlPlaneRuntimeNodeSecretProtector
{
    private readonly IDataProtector _protector;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataProtectionControlPlaneRuntimeNodeSecretProtector"/> class.
    /// </summary>
    public DataProtectionControlPlaneRuntimeNodeSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Krackend.Sagas.Orchestrations.ControlPlane.RuntimeNodes.Secrets.v1");
    }

    /// <inheritdoc />
    public string Protect(string secret)
        => _protector.Protect(secret ?? string.Empty);

    /// <inheritdoc />
    public string Unprotect(string protectedSecret)
        => _protector.Unprotect(protectedSecret ?? string.Empty);
}
