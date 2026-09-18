using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;

internal sealed class ControlPlaneEntityFrameworkModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        if (context is ControlPlaneDbContext controlPlaneDbContext)
        {
            return (context.GetType(), controlPlaneDbContext.ModelCustomizationCacheKey, designTime);
        }

        return (context.GetType(), designTime);
    }
}
