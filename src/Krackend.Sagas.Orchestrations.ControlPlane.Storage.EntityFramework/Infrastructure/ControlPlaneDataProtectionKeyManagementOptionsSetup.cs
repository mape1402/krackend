using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;

internal sealed class ControlPlaneDataProtectionKeyManagementOptionsSetup : IConfigureOptions<KeyManagementOptions>
{
    private readonly ControlPlaneDataProtectionKeyXmlRepository repository;

    public ControlPlaneDataProtectionKeyManagementOptionsSetup(ControlPlaneDataProtectionKeyXmlRepository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public void Configure(KeyManagementOptions options)
    {
        options.XmlRepository = repository;
    }
}
