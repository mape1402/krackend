using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

internal sealed class RuntimeDataProtectionKeyManagementOptionsSetup : IConfigureOptions<KeyManagementOptions>
{
    private readonly RuntimeDataProtectionKeyXmlRepository repository;

    public RuntimeDataProtectionKeyManagementOptionsSetup(RuntimeDataProtectionKeyXmlRepository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public void Configure(KeyManagementOptions options)
    {
        options.XmlRepository = repository;
    }
}
