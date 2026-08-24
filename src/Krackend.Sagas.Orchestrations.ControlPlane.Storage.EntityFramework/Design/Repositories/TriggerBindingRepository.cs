using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Mappings;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Repositories;

/// <summary>
/// Represents TriggerBindingRepository.
/// </summary>
public sealed class TriggerBindingRepository : ITriggerBindingRepository
{
    private readonly ControlPlaneDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="dbContext">The dbContext value.</param>
    public TriggerBindingRepository(ControlPlaneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Executes Create.
    /// </summary>
    public async Task Create(TriggerBinding triggerBinding, CancellationToken cancellationToken = default)
    {
        _dbContext.TriggerBindings.Add(triggerBinding.ToEntity());
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes Update.
    /// </summary>
    public async Task Update(TriggerBinding triggerBinding, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.TriggerBindings.FirstOrDefaultAsync(x => x.Id == triggerBinding.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"TriggerBinding '{triggerBinding.Id}' was not found.");

        var next = triggerBinding.ToEntity();
        _dbContext.Entry(current).CurrentValues.SetValues(next);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes Delete.
    /// </summary>
    public async Task Delete(Id triggerBindingId, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.TriggerBindings.FirstOrDefaultAsync(x => x.Id == triggerBindingId, cancellationToken)
            ?? throw new KeyNotFoundException($"TriggerBinding '{triggerBindingId}' was not found.");

        _dbContext.TriggerBindings.Remove(current);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes SetIsEnabled.
    /// </summary>
    public async Task SetIsEnabled(Id triggerBindingId, bool isEnabled, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.TriggerBindings.FirstOrDefaultAsync(x => x.Id == triggerBindingId, cancellationToken)
            ?? throw new KeyNotFoundException($"TriggerBinding '{triggerBindingId}' was not found.");

        current.IsEnabled = isEnabled;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes GetAll.
    /// </summary>
    public async Task<IEnumerable<TriggerBinding>> GetAll(Id orchestrationVersionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TriggerBindings
            .AsNoTracking()
            .Where(x => x.OrchestrationVersionId == orchestrationVersionId)
            .OrderBy(x => x.Id)
            .Select(x => x.ToDefinition())
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Executes GetById.
    /// </summary>
    public async Task<TriggerBinding> GetById(Id triggerBindingId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.TriggerBindings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == triggerBindingId, cancellationToken)
            ?? throw new KeyNotFoundException($"TriggerBinding '{triggerBindingId}' was not found.");

        return entity.ToDefinition();
    }
}
