using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Infrastructure;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Repositories;

internal static class RuntimeRepositorySaveExtensions
{
    public static Task SaveChangesIfNeeded(this RuntimeStorageDbContext dbContext, CancellationToken cancellationToken)
        => dbContext.AutoSaveChanges ? dbContext.SaveChangesAsync(cancellationToken) : Task.CompletedTask;

    public static void ApplyValues<TEntity>(this RuntimeStorageDbContext dbContext, DbSet<TEntity> set, object id, TEntity values)
        where TEntity : class
    {
        var tracked = set.Local.FirstOrDefault(entity => Equals(dbContext.Entry(entity).Property("Id").CurrentValue, id));
        if (tracked is null)
        {
            tracked = values;
            set.Attach(tracked);
            dbContext.Entry(tracked).State = EntityState.Modified;
        }

        var entry = dbContext.Entry(tracked);
        entry.CurrentValues.SetValues(values);
        if (entry.State != EntityState.Added)
            entry.State = EntityState.Modified;
    }
}
