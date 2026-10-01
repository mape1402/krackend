using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;

internal sealed class ControlPlaneDataProtectionKeyXmlRepository : IXmlRepository
{
    private readonly IServiceScopeFactory scopeFactory;

    public ControlPlaneDataProtectionKeyXmlRepository(IServiceScopeFactory scopeFactory)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
        var keyXml = dbContext.DataProtectionKeys
            .AsNoTracking()
            .Select(x => x.Xml)
            .ToArray();

        return keyXml.Select(XElement.Parse).ToArray();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
        dbContext.DataProtectionKeys.Add(new ControlPlaneDataProtectionKeyEntity
        {
            FriendlyName = friendlyName ?? string.Empty,
            Xml = element.ToString(SaveOptions.DisableFormatting)
        });
        dbContext.SaveChanges();
    }
}
