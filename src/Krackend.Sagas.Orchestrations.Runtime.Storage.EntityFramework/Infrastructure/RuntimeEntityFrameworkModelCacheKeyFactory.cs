using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

internal sealed class RuntimeEntityFrameworkModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        if (context is RuntimeDbContext runtimeDbContext)
        {
            return (context.GetType(), runtimeDbContext.ModelCustomizationCacheKey, designTime);
        }

        return (context.GetType(), designTime);
    }
}
