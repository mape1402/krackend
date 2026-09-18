using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Mule;
using Mule.Dispatching;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Actions;

/// <summary>
/// Schedules artifact projection work by writing Mule durable actions through the runtime DbContext.
/// </summary>
internal sealed class EntityFrameworkRuntimeArtifactProjectionScheduler : IRuntimeArtifactProjectionScheduler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly RuntimeDbContext _dbContext;
    private readonly IMuleCommitNotifier _commitNotifier;
    private readonly IRuntimeStorageUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkRuntimeArtifactProjectionScheduler"/> class.
    /// </summary>
    public EntityFrameworkRuntimeArtifactProjectionScheduler(
        RuntimeDbContext dbContext,
        IMuleCommitNotifier commitNotifier,
        IRuntimeStorageUnitOfWork unitOfWork)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _commitNotifier = commitNotifier ?? throw new ArgumentNullException(nameof(commitNotifier));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <inheritdoc />
    public async Task ScheduleProjectionAsync(
        RuntimeArtifactProjectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var key = ActionKey.From(RuntimeArtifactActionNames.ProjectIngressConfigurations);
        var deduplicationKey = $"{request.ArtifactId}:{request.IngressGeneration}";
        var current = await _dbContext.Set<DurableAction>().FirstOrDefaultAsync(
            x => x.Key == key && x.DeduplicationKey == deduplicationKey,
            cancellationToken);

        if (current is not null)
        {
            if (current.Status == DurableActionStatus.Failed)
            {
                current.Status = DurableActionStatus.Pending;
                current.Attempts = 0;
                current.LastError = null;
                current.LockedOnUtc = null;
                current.StartedOnUtc = null;
                current.NextAttemptOnUtc = null;
                current.CompletedOnUtc = null;
                current.TerminalOnUtc = null;

                await SaveAndNotifyIfNeededAsync(
                    current.Id,
                    key,
                    deduplicationKey,
                    current.Lane,
                    cancellationToken);
            }

            return;
        }

        var action = new DurableAction
        {
            Id = Guid.NewGuid(),
            Key = key,
            Lane = RuntimeArtifactProjectionSchedulerDefaults.Lane,
            Payload = JsonSerializer.Serialize(request, SerializerOptions),
            PayloadType = typeof(RuntimeArtifactProjectionRequest).AssemblyQualifiedName!,
            Metadata = null,
            CorrelationId = request.ArtifactId,
            DeduplicationKey = deduplicationKey,
            Status = DurableActionStatus.Pending,
            Attempts = 0,
            LastError = null,
            CreatedOnUtc = DateTimeOffset.UtcNow,
            LockedOnUtc = null,
            StartedOnUtc = null,
            NextAttemptOnUtc = null,
            CompletedOnUtc = null,
            TerminalOnUtc = null
        };

        _dbContext.Set<DurableAction>().Add(action);

        await SaveAndNotifyIfNeededAsync(
            action.Id,
            key,
            deduplicationKey,
            action.Lane,
            cancellationToken);
    }

    private async Task SaveAndNotifyIfNeededAsync(
        Guid actionId,
        ActionKey key,
        string deduplicationKey,
        string lane,
        CancellationToken cancellationToken)
    {
        if (!_unitOfWork.AutoSaveChanges)
        {
            return;
        }

        try
        {
            await _unitOfWork.SaveChanges(cancellationToken);
            await _commitNotifier.NotifySavedAsync(actionId, lane, cancellationToken);
        }
        catch (DbUpdateException)
        {
            DetachPendingAction(actionId);

            var existing = await _dbContext.Set<DurableAction>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Key == key && x.DeduplicationKey == deduplicationKey,
                    cancellationToken);

            if (existing is null)
            {
                throw;
            }

            await _commitNotifier.NotifySavedAsync(existing.Id, existing.Lane, cancellationToken);
        }
    }

    private void DetachPendingAction(Guid actionId)
    {
        var entries = _dbContext.ChangeTracker.Entries<DurableAction>()
            .Where(entry => entry.Entity.Id == actionId && entry.State == EntityState.Added)
            .ToArray();

        foreach (var entry in entries)
        {
            entry.State = EntityState.Detached;
        }
    }
}
