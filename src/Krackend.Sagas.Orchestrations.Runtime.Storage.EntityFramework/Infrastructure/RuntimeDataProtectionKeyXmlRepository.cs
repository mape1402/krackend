using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

internal sealed class RuntimeDataProtectionKeyXmlRepository : IXmlRepository
{
    private readonly IServiceScopeFactory scopeFactory;

    public RuntimeDataProtectionKeyXmlRepository(IServiceScopeFactory scopeFactory)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var keyXml = dbContext.DataProtectionKeys
            .AsNoTracking()
            .Select(x => x.Xml)
            // IXmlRepository is a synchronous ASP.NET Core contract, but providers
            // such as Npgsql multiplexing intentionally reject synchronous commands.
            // Start the provider's asynchronous pipeline and bridge only at this
            // infrequent key-ring boundary.
            .ToArrayAsync()
            .GetAwaiter()
            .GetResult();

        return keyXml.Select(XElement.Parse).ToArray();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        dbContext.DataProtectionKeys.Add(new RuntimeDataProtectionKeyEntity
        {
            FriendlyName = friendlyName ?? string.Empty,
            Xml = element.ToString(SaveOptions.DisableFormatting)
        });
        dbContext.SaveChangesAsync()
            .GetAwaiter()
            .GetResult();
    }
}
