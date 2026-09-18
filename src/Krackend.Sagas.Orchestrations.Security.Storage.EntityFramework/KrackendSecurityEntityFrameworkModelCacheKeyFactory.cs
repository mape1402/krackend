using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Krackend.Sagas.Orchestrations.Security.Storage.EntityFramework;

internal sealed class KrackendSecurityEntityFrameworkModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        if (context is KrackendSecurityDbContext securityDbContext)
        {
            return (context.GetType(), securityDbContext.ModelCustomizationCacheKey, designTime);
        }

        return (context.GetType(), designTime);
    }
}
