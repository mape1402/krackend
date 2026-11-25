using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Extensions;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mule;
using Mule.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

public sealed class RuntimeDbContext : DbContext
{
    private readonly RuntimeEntityFrameworkStorageOptions storageOptions;
    private readonly RuntimeMutationLeaseScope _mutationLease;
    private Guid? _guardedTransactionId;
    private Id? _guardedInstanceId;
    private string _guardedLeaseId;
    private bool _guardedByTransactionLock;
    private int _deferredProviderDurabilityCount;

    internal object ModelCustomizationCacheKey
        => (storageOptions.ConfigureModel, storageOptions.CompressSnapshotPayload);

    internal bool IsProviderDurabilityDeferred => _deferredProviderDurabilityCount > 0;

    internal IDisposable DeferProviderDurability()
    {
        _deferredProviderDurabilityCount++;
        return new ProviderDurabilityDeferral(this);
    }

    public RuntimeDbContext(DbContextOptions<RuntimeDbContext> options)
        : this(
            options,
            options.FindExtension<RuntimeDbContextOptionsExtension>()?.StorageOptions ??
                new RuntimeEntityFrameworkStorageOptions(),
            options.FindExtension<RuntimeDbContextOptionsExtension>()?.MutationLease)
    {
    }

    private RuntimeDbContext(
        DbContextOptions<RuntimeDbContext> options,
        RuntimeEntityFrameworkStorageOptions storageOptions,
        RuntimeMutationLeaseScope mutationLease)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(storageOptions);
        this.storageOptions = storageOptions;
        _mutationLease = mutationLease;

        // Every engine activation owns its transaction and performs one final
        // SaveChanges. EF's nested automatic savepoint therefore cannot provide a
        // partial rollback boundary, but it does add a command to every activation
        // and makes the exact same transaction incompatible with SQL Server's
        // durable memory-optimized tables. On failure the owning transaction is
        // disposed and rolled back in full by RuntimeStorageUnitOfWork.
        Database.AutoSavepointsEnabled = false;
    }

    public static RuntimeDbContext Create(
        DbContextOptions<RuntimeDbContext> options,
        IOptions<RuntimeEntityFrameworkStorageOptions> storageOptions,
        RuntimeMutationLeaseScope mutationLease = null)
        => new(options, storageOptions?.Value ?? throw new ArgumentNullException(nameof(storageOptions)), mutationLease);

    public DbSet<RuntimeOrchestrationArtifact> RuntimeOrchestrationArtifacts => Set<RuntimeOrchestrationArtifact>();

    public DbSet<RuntimeArtifactDeploymentRecord> RuntimeArtifactDeploymentHistory => Set<RuntimeArtifactDeploymentRecord>();

    public DbSet<RuntimeTriggerClaim> RuntimeTriggerClaims => Set<RuntimeTriggerClaim>();

    public DbSet<RuntimeDesignNode> RuntimeDesignNodes => Set<RuntimeDesignNode>();

    public DbSet<OrchestrationInstance> OrchestrationInstances => Set<OrchestrationInstance>();

    internal DbSet<SagaStatisticsObservation> SagaStatisticsObservations => Set<SagaStatisticsObservation>();

    internal DbSet<SagaStatisticsProjection> SagaStatisticsProjections => Set<SagaStatisticsProjection>();

    internal DbSet<SagaStatisticsBackfillCheckpoint> SagaStatisticsBackfillCheckpoints => Set<SagaStatisticsBackfillCheckpoint>();

    internal DbSet<SagaFailureEvent> SagaFailureEvents => Set<SagaFailureEvent>();
    internal DbSet<SagaFailureBackfillCheckpoint> SagaFailureBackfillCheckpoints => Set<SagaFailureBackfillCheckpoint>();

    public DbSet<StageExecution> StageExecutions => Set<StageExecution>();

    public DbSet<TaskExecution> TaskExecutions => Set<TaskExecution>();

    public DbSet<TaskExecutionAttempt> TaskExecutionAttempts => Set<TaskExecutionAttempt>();

    public DbSet<TaskDispatch> TaskDispatches => Set<TaskDispatch>();

    public DbSet<ExecutionTransition> ExecutionTransitions => Set<ExecutionTransition>();

    public DbSet<InstanceVariable> InstanceVariables => Set<InstanceVariable>();

    public DbSet<EnvironmentVariableValue> EnvironmentVariableValues => Set<EnvironmentVariableValue>();

    public DbSet<CompensationExecution> CompensationExecutions => Set<CompensationExecution>();

    public DbSet<RuntimeIngressConfiguration> RuntimeIngressConfigurations => Set<RuntimeIngressConfiguration>();

    public DbSet<RuntimeExtensionPackage> RuntimeExtensionPackages => Set<RuntimeExtensionPackage>();

    public DbSet<RuntimeDataProtectionKeyEntity> DataProtectionKeys => Set<RuntimeDataProtectionKeyEntity>();

    internal void MarkMutationLeaseGuarded(
        Id instanceId,
        string leaseId,
        bool guardedByTransactionLock = false)
    {
        var transactionId = Database.CurrentTransaction?.TransactionId;
        if (!transactionId.HasValue) return;

        _guardedTransactionId = transactionId;
        _guardedInstanceId = instanceId;
        _guardedLeaseId = leaseId;
        _guardedByTransactionLock = guardedByTransactionLock;
    }

    internal bool IsGuardedByTransactionLock(Id instanceId, string leaseId)
        => _guardedByTransactionLock &&
            _guardedTransactionId == Database.CurrentTransaction?.TransactionId &&
            _guardedInstanceId == instanceId &&
            string.Equals(_guardedLeaseId, leaseId, StringComparison.Ordinal);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction ownedTransaction = null;
        try
        {
            if (_mutationLease?.Current is { } lease && Database.IsRelational())
            {
                if (Database.CurrentTransaction is null)
                    ownedTransaction = await Database.BeginTransactionAsync(cancellationToken);
                var transactionId = Database.CurrentTransaction?.TransactionId;
                var alreadyGuarded = transactionId.HasValue &&
                    _guardedTransactionId == transactionId &&
                    _guardedInstanceId == lease.InstanceId &&
                    string.Equals(_guardedLeaseId, lease.LeaseId, StringComparison.Ordinal);
                if (!alreadyGuarded)
                {
                    // The conditional write locks this instance row until the activation
                    // transaction commits. Once that row lock is held, repeating the same
                    // guard before every decision save only adds SQL traffic; takeover cannot
                    // succeed inside the transaction.
                    var guarded = await OrchestrationInstances.Where(x => x.Id == lease.InstanceId &&
                            x.ActiveLeaseId == lease.LeaseId && x.ActiveLeaseExpiresOnUtc > DateTime.UtcNow)
                        .ExecuteUpdateAsync(update => update.SetProperty(x => x.ActiveLeaseId, lease.LeaseId), cancellationToken);
                    if (guarded != 1) throw new RuntimeStateConflictException($"The mutation no longer owns the lease for '{lease.InstanceId}'.");
                    _guardedTransactionId = transactionId;
                    _guardedInstanceId = lease.InstanceId;
                    _guardedLeaseId = lease.LeaseId;
                }
            }
            var affected = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            if (ownedTransaction is not null) await ownedTransaction.CommitAsync(cancellationToken);
            return affected;
        }
        catch (DbUpdateConcurrencyException exception) when (_mutationLease?.Current is not null || exception.Entries.Any(x => x.Entity is TaskDispatch))
        {
            ChangeTracker.Clear();
            throw new RuntimeStateConflictException("Runtime state changed concurrently. Reload the action before continuing.", exception);
        }
        catch (RuntimeStateConflictException) { ChangeTracker.Clear(); throw; }
        finally { if (ownedTransaction is not null) await ownedTransaction.DisposeAsync(); }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("Runtime");
        ConfigureRuntimeArtifacts(modelBuilder);
        ConfigureRuntimeArtifactDeploymentHistory(modelBuilder);
        ConfigureRuntimeTriggerClaims(modelBuilder);
        ConfigureRuntimeDesignNodes(modelBuilder);
        ConfigureInstances(modelBuilder);
        ConfigureStatisticsObservations(modelBuilder);
        ConfigureStatisticsProjections(modelBuilder);
        ConfigureStatisticsBackfill(modelBuilder);
        ConfigureFailureStatistics(modelBuilder);
        ConfigureStages(modelBuilder);
        ConfigureTasks(modelBuilder);
        ConfigureAttempts(modelBuilder);
        ConfigureDispatches(modelBuilder);
        ConfigureTransitions(modelBuilder);
        ConfigureVariables(modelBuilder);
        ConfigureCompensations(modelBuilder);
        ConfigureIngressConfigurations(modelBuilder);
        ConfigureRuntimeExtensionPackages(modelBuilder);
        ConfigureDataProtectionKeys(modelBuilder);
        modelBuilder.UseMuleModel();
        // Runtime diagnostics and cancellation recovery resolve durable work by saga
        // correlation. Keep those lookups independent of the accumulated action history.
        modelBuilder.Entity<DurableAction>().HasIndex(x => x.CorrelationId);
        base.OnModelCreating(modelBuilder);
        storageOptions.ConfigureModel?.Invoke(modelBuilder);
    }

    private static void ConfigureDataProtectionKeys(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<RuntimeDataProtectionKeyEntity>();
        builder.ToTable("DataProtectionKeys");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FriendlyName).HasMaxLength(256);
        builder.Property(x => x.Xml).IsRequired();
    }

    private sealed class ProviderDurabilityDeferral(RuntimeDbContext context) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            context._deferredProviderDurabilityCount--;
            _disposed = true;
        }
    }

    private static void ConfigureRuntimeDesignNodes(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<RuntimeDesignNode>();
        builder.ToTable("RuntimeDesignNodes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.EndpointBaseUri).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.RemoteRuntimeNodeId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DistributionMode).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.InboundClientId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.InboundKeyId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.InboundSecretHash).IsRequired(false);
        builder.Property(x => x.InboundAllowedScopes).HasMaxLength(512).IsRequired(false);
        builder.Property(x => x.InboundCredentialStatus).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.InboundLastFailureReason).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.OutboundClientId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.OutboundKeyId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.ProtectedOutboundSecret).IsRequired(false);
        builder.Property(x => x.OutboundRequestedScopes).HasMaxLength(512).IsRequired(false);
        builder.Property(x => x.OutboundCredentialStatus).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(64)
            .HasDefaultValue(RuntimeDesignNodeStatus.Pending)
            .HasSentinel(RuntimeDesignNodeStatus.Pending)
            .IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();
        builder.HasIndex(x => x.InboundClientId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.IsEnabled);
    }

    private static void ConfigureRuntimeArtifacts(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<RuntimeOrchestrationArtifact>();
        builder.ToTable("RuntimeOrchestrationArtifacts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationDefinitionKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ArtifactType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SourceOrchestrationVersionId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Version).HasMaxLength(64).HasConversion(new SemanticVersionConverter()).IsRequired();
        builder.Property(x => x.ArtifactChecksum).HasMaxLength(256).HasConversion(new ChecksumConverter()).IsRequired();
        builder.Property(x => x.ArtifactPayload).HasConversion(new JsonNodeConverter()).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).HasDefaultValue(RuntimeOrchestrationArtifactStatus.Pending).IsRequired().IsConcurrencyToken();
        builder.Property(x => x.IngressGeneration).IsConcurrencyToken();
        builder.Property(x => x.LifecycleStatus).HasConversion<string>().HasMaxLength(64).HasDefaultValue(RuntimeOrchestrationLifecycleStatus.Active).IsRequired();
        builder.Property(x => x.LastReleaseId).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.LastControlPlaneArtifactId).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.ReleaseStrategy).HasMaxLength(64).IsRequired(false);
        builder.Property(x => x.ProjectionError).HasMaxLength(4000).IsRequired(false);
        builder.Property(x => x.SupersededByArtifactId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Notes).HasMaxLength(2000).IsRequired(false);
        builder.HasIndex(x => new { x.OrchestrationDefinitionKey, x.Version }).IsUnique();
        builder.HasIndex(x => new { x.OrchestrationDefinitionKey, x.IsActive });
        builder.HasIndex(x => new { x.Status, x.IsActive });
        builder.HasIndex(x => new { x.LifecycleStatus, x.IsActive });
    }

    private static void ConfigureRuntimeArtifactDeploymentHistory(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<RuntimeArtifactDeploymentRecord>();
        builder.ToTable("RuntimeArtifactDeploymentHistory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.RuntimeOrchestrationArtifactId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationDefinitionKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.OrchestrationVersion).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ControlPlaneArtifactId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ReleaseId).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.ArtifactType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ReleaseStrategy).HasMaxLength(64).IsRequired();
        builder.Property(x => x.LifecycleStatus).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.RuntimeStatus).HasConversion<string>().HasMaxLength(64).IsRequired().IsConcurrencyToken();
        builder.HasIndex(x => new { x.OrchestrationDefinitionKey, x.AppliedOnUtc });
        builder.HasIndex(x => x.ControlPlaneArtifactId).IsUnique();
        builder.HasIndex(x => x.ReleaseId);
    }

    private static void ConfigureRuntimeTriggerClaims(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<RuntimeTriggerClaim>();
        builder.ToTable("RuntimeTriggerClaims");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(1024).IsRequired();
        // Ownership changes must compare the previously read owner so concurrent
        // rollouts cannot both replace the same trigger claim.
        builder.Property(x => x.RuntimeOrchestrationArtifactId).HasConversion(new IdToBytesConverter()).IsConcurrencyToken();
        builder.HasIndex(x => x.RuntimeOrchestrationArtifactId);
    }

    private void ConfigureInstances(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<OrchestrationInstance>();
        builder.ToTable("OrchestrationInstances");
        // Leave room for PostgreSQL HOT tuples. One orchestration row is updated for
        // every callback; packing pages to 100% turns those independent mutations into
        // heap churn. Other providers safely ignore this provider annotation.
        builder.HasAnnotation("Npgsql:StorageParameter:fillfactor", 30);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.RuntimeOrchestrationArtifactId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.TriggerIntakeId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.StartIdempotencyKey).HasMaxLength(512).IsRequired(false);
        builder.Property(x => x.OrchestrationDefinitionKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.OrchestrationVersion).HasMaxLength(64).IsRequired(false);
        builder.Property(x => x.CorrelationId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.SagaId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ExecutionKey).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Status)
            .UsePropertyAccessMode(PropertyAccessMode.Property)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(x => x.StatisticsRevision).HasDefaultValue(0L).IsConcurrencyToken();
        builder.Property(x => x.CurrentStageKey).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.CurrentTaskKey).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.LastExecutedStageKey).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.LastExecutedTaskKey).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.CurrentParallelGroupKey).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.FinalOutcome).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.ErrorSummary).HasMaxLength(4000).IsRequired(false);
        builder.Property(x => x.ActiveLeaseId).HasMaxLength(256).IsRequired(false).IsConcurrencyToken();
        builder.Property(x => x.ActiveLeaseExpiresOnUtc).IsConcurrencyToken();
        builder.Property(x => x.SnapshotPayload)
            .HasConversion(storageOptions.CompressSnapshotPayload
                ? new CompressedJsonNodeConverter()
                : new JsonNodeConverter())
            .IsRequired(false);
        builder.Property(x => x.Metadata).HasConversion(new JsonNodeDictionaryConverter(), new JsonNodeDictionaryComparer()).IsRequired(false);
        builder.HasIndex(x => x.StatisticsRevision);
        // Instance catalogs page and order by the immutable start timestamp while
        // executions move through every status. The Running-only lifetime index
        // below cannot serve that query once rows complete, which otherwise turns
        // a live dashboard into repeated table scans competing with saga commits.
        // StartedOnUtc never changes, so this index is maintained only on ingress.
        builder.HasIndex(x => new { x.StartedOnUtc, x.Id })
            .HasDatabaseName("IX_OrchestrationInstances_StartedOnUtc_Id")
            // Memory-optimized nonclustered indexes cannot be scanned in reverse.
            // The live catalogs show newest instances first, so their physical
            // index direction must match that query exactly.
            .IsDescending(true, true);
        builder.HasIndex(x => new { x.Status, x.ExpiresOnUtc, x.Id });
        builder.HasIndex(x => new { x.Status, x.IdleExpiresOnUtc, x.Id });
        // LastUpdatedOnUtc changes on every state mutation. Indexing it globally (or
        // behind Status) turns every independent saga into a delete + right-edge insert
        // against the same B-tree page. Recent/catalog reads are intentionally allowed
        // to sort their projection instead of imposing cross-saga write serialization.
        // Cancellation recovery is exceptional work. Keeping every healthy instance
        // (all sharing the leading value false) in this index turns LastUpdatedOnUtc
        // into a right-edge latch convoy under normal callback traffic.
        // LastUpdatedOnUtc changes on every callback. Even behind a partial predicate,
        // PostgreSQL must treat that column as indexed and therefore cannot perform a
        // HOT update for healthy instances. Cancellation candidates are exceptional
        // and few; index only their stable identity and sort that tiny set in memory.
        builder.HasIndex(x => x.StartIdempotencyKey).IsUnique();
        builder.HasIndex(x => x.CorrelationId)
            // Prefix correlation scans drive live status observation and load-test
            // completion checks. PostgreSQL's default locale operator class cannot
            // use a normal varchar index for LIKE 'prefix%'. Equality remains usable.
            .HasAnnotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });
    }

    private static void ConfigureStatisticsObservations(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<SagaStatisticsObservation>();
        builder.ToTable("SagaStatisticsObservations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(64);
        builder.Property(x => x.InstanceId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.ArtifactId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.StageKey).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.Processed).IsConcurrencyToken();
        builder.HasIndex(x => new { x.InstanceId, x.Revision }).IsUnique();
        // Only unprocessed observations are recovery work. Retaining processed rows
        // in this index makes every projection a second hot-tail insert and forces
        // unrelated saga statistics to contend with normal engine commits.
        builder.HasIndex(x => new { x.OrchestrationKey, x.OccurredOnUtc });
    }

    private static void ConfigureStatisticsProjections(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<SagaStatisticsProjection>();
        builder.ToTable("SagaStatisticsProjections");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(26);
        builder.Property(x => x.ArtifactId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.HasIndex(x => new { x.OrchestrationKey, x.StartedOnUtc });
        builder.HasIndex(x => new { x.Status, x.StartedOnUtc });
    }

    private static void ConfigureStatisticsBackfill(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<SagaStatisticsBackfillCheckpoint>();
        builder.ToTable("SagaStatisticsBackfillCheckpoints");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(64);
    }

    private static void ConfigureFailureStatistics(ModelBuilder modelBuilder)
    {
        var events = modelBuilder.Entity<SagaFailureEvent>();
        events.ToTable("SagaFailureEvents");
        events.HasKey(x => x.Id);
        events.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        events.Property(x => x.OrchestrationInstanceId).HasConversion(new IdToBytesConverter());
        events.Property(x => x.TransitionType).HasMaxLength(128).IsRequired();
        events.Property(x => x.OrchestrationKey).HasMaxLength(256);
        events.Property(x => x.OrchestrationVersion).HasMaxLength(64);
        events.HasIndex(x => new { x.TransitionType, x.OccurredOnUtc });
        events.HasIndex(x => new { x.OrchestrationKey, x.OrchestrationVersion, x.TransitionType, x.OccurredOnUtc });
        events.HasIndex(x => new { x.ScopeProjectionVersion, x.OccurredOnUtc });
        var checkpoints = modelBuilder.Entity<SagaFailureBackfillCheckpoint>();
        checkpoints.ToTable("SagaFailureBackfillCheckpoints");
        checkpoints.HasKey(x => x.Id);
        checkpoints.Property(x => x.Id).HasMaxLength(64);
        checkpoints.Property(x => x.ConcurrencyToken).HasMaxLength(32).IsConcurrencyToken().IsRequired();
    }

    private static void ConfigureStages(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<StageExecution>();
        builder.ToTable("StageExecutions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationInstanceId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.StageKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.SkipReason).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.ErrorSummary).HasMaxLength(4000).IsRequired(false);
        builder.Property(x => x.Metadata).HasConversion(new JsonNodeDictionaryConverter(), new JsonNodeDictionaryComparer()).IsRequired(false);
        builder.HasIndex(x => new { x.OrchestrationInstanceId, x.StageKey }).IsUnique();
    }

    private static void ConfigureTasks(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<TaskExecution>();
        builder.ToTable("TaskExecutions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationInstanceId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.StageExecutionId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.ParallelGroupId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.TaskKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.TaskKind).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExecutionMode).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.OnErrorPolicy).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.SkipReason).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.OutputVariablesPayload).HasConversion(new JsonNodeConverter()).IsRequired(false);
        builder.Property(x => x.CorrelationId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.TimeoutActionId).HasMaxLength(64).IsRequired(false);
        builder.Property(x => x.TimeoutActionLane).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.Metadata).HasConversion(new JsonNodeDictionaryConverter(), new JsonNodeDictionaryComparer()).IsRequired(false);
        builder.HasIndex(x => new { x.StageExecutionId, x.TaskKey }).IsUnique();
        // Decision evaluation and recovery load the complete task graph by instance.
        // Without this index every transition scans all historical task executions,
        // making unrelated sagas slow each other down as the runtime grows.
        builder.HasIndex(x => x.OrchestrationInstanceId);
        builder.HasIndex(x => x.CorrelationId);
        // Deadline recovery is driven by the task row. Keep the healthy write path to
        // one timeout index and order it exactly like the scanner: the previous
        // Status/CorrelationId/Deadline shape forced a scan and duplicated the same
        // WaitingResponse churn on the attempt table.
        builder.HasIndex(x => new { x.ResponseDeadlineUtc, x.Id })
            .HasDatabaseName("IX_TaskExecutions_Waiting_ResponseDeadlineUtc_Id");
        // Legacy rows created before deadlines were projected still need bounded
        // recovery without making every normal awaited task maintain a second index.
        builder.HasIndex(x => new { x.WaitingSinceUtc, x.Id })
            .HasDatabaseName("IX_TaskExecutions_LegacyWaiting_WaitingSinceUtc_Id");
    }

    private static void ConfigureAttempts(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<TaskExecutionAttempt>();
        builder.ToTable("TaskExecutionAttempts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.TaskExecutionId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.DispatchId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.RequestPayload).HasConversion(new JsonNodeConverter()).IsRequired(false);
        builder.Property(x => x.ResponsePayload).HasConversion(new JsonNodeConverter()).IsRequired(false);
        builder.Property(x => x.ErrorCode).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.ErrorMessage).HasMaxLength(4000).IsRequired(false);
        builder.Property(x => x.Metadata).HasConversion(new JsonNodeDictionaryConverter(), new JsonNodeDictionaryComparer()).IsRequired(false);
        builder.HasIndex(x => new { x.TaskExecutionId, x.AttemptNumber }).IsUnique();
        builder.HasIndex(x => x.DispatchId);
        // Timeout ownership lives on TaskExecution. Indexing the same transient status
        // here doubled the insert/delete and latch cost of every successful callback.
    }

    private static void ConfigureDispatches(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<TaskDispatch>();
        builder.ToTable("TaskDispatches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.TaskExecutionAttemptId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.DispatchType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Destination).HasMaxLength(512).IsRequired(false);
        builder.Property(x => x.RequestPayload).HasConversion(new JsonNodeConverter()).IsRequired(false);
        builder.Property(x => x.DispatchStatus).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CommandId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.CorrelationId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.FailureReason).HasMaxLength(4000).IsRequired(false);
        builder.Property(x => x.PhysicalCancellationPending).IsRequired();
        builder.Property(x => x.CallbackReceiptId).HasMaxLength(64).IsRequired(false).IsConcurrencyToken();
        builder.Property(x => x.CallbackAcceptedOnUtc).IsRequired(false);
        builder.Property(x => x.PendingCallbackPayload).IsRequired(false);
        builder.Property(x => x.Metadata).HasConversion(new JsonNodeDictionaryConverter(), new JsonNodeDictionaryComparer()).IsRequired(false);
        builder.HasIndex(x => x.TaskExecutionAttemptId).IsUnique();
        builder.HasIndex(x => x.CommandId);
        // QueuePending is the only status queried as a recovery set. The random
        // storage prefix of Id spreads concurrent inserts; correlation ids often
        // share a long run prefix and created a right-edge latch convoy.
        builder.HasIndex(x => new { x.ScheduledOnUtc, x.Id })
            .HasDatabaseName("IX_TaskDispatches_QueuePending_Id_ScheduledOnUtc");
        builder.HasIndex(x => x.PhysicalCancellationPending);
        builder.HasIndex(x => new { x.CallbackPending, x.CallbackAcceptedOnUtc });
    }

    private static void ConfigureTransitions(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<ExecutionTransition>();
        builder.ToTable("ExecutionTransitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationInstanceId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.StageExecutionId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.TaskExecutionId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.TaskExecutionAttemptId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.TransitionType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.FromStatus).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.ToStatus).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.Message).HasMaxLength(4000).IsRequired(false);
        builder.Property(x => x.Payload).HasConversion(new JsonNodeConverter()).IsRequired(false);
        builder.Property(x => x.ProducedBy).HasMaxLength(256).IsRequired(false);
        builder.HasIndex(x => new { x.OrchestrationInstanceId, x.OccurredOnUtc });
        builder.HasIndex(x => x.OccurredOnUtc);
    }

    private static void ConfigureVariables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InstanceVariable>().ToTable("InstanceVariables").HasKey(x => x.Id);
        modelBuilder.Entity<InstanceVariable>().Property(x => x.Id).HasConversion(new IdToBytesConverter());
        modelBuilder.Entity<InstanceVariable>().Property(x => x.OrchestrationInstanceId).HasConversion(new IdToBytesConverter());
        modelBuilder.Entity<InstanceVariable>().Property(x => x.Key).HasMaxLength(256).IsRequired();
        modelBuilder.Entity<InstanceVariable>().Property(x => x.Scope).HasConversion<string>().HasMaxLength(64).IsRequired();
        modelBuilder.Entity<InstanceVariable>().Property(x => x.ValueType).HasConversion<string>().HasMaxLength(64).IsRequired();
        modelBuilder.Entity<InstanceVariable>().Property(x => x.Value).HasConversion(new JsonNodeConverter()).IsRequired(false);
        modelBuilder.Entity<InstanceVariable>().Property(x => x.SourceType).HasMaxLength(128).IsRequired();
        modelBuilder.Entity<InstanceVariable>().Property(x => x.SourceReference).HasMaxLength(512).IsRequired(false);
        modelBuilder.Entity<InstanceVariable>().Property(x => x.LastUpdatedBy).HasMaxLength(128).IsRequired(false);
        modelBuilder.Entity<InstanceVariable>().HasIndex(x => new { x.OrchestrationInstanceId, x.Key }).IsUnique();

        modelBuilder.Entity<EnvironmentVariableValue>().ToTable("EnvironmentVariableValues").HasKey(x => x.Id);
        modelBuilder.Entity<EnvironmentVariableValue>().Property(x => x.Id).HasConversion(new IdToBytesConverter());
        modelBuilder.Entity<EnvironmentVariableValue>().Property(x => x.VariableKey).HasMaxLength(256).IsRequired();
        modelBuilder.Entity<EnvironmentVariableValue>().Property(x => x.ValueType).HasConversion<string>().HasMaxLength(64).IsRequired();
        modelBuilder.Entity<EnvironmentVariableValue>().Property(x => x.Value).HasConversion(new JsonNodeConverter()).IsRequired(false);
        modelBuilder.Entity<EnvironmentVariableValue>().Property(x => x.UpdatedBy).HasMaxLength(128).IsRequired(false);
        modelBuilder.Entity<EnvironmentVariableValue>().Property(x => x.Notes).HasMaxLength(2000).IsRequired(false);
        modelBuilder.Entity<EnvironmentVariableValue>().HasIndex(x => x.VariableKey).IsUnique();
    }

    private static void ConfigureCompensations(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<CompensationExecution>();
        builder.ToTable("CompensationExecutions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationInstanceId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.SourceTaskExecutionId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.CompensationTaskKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RequestPayload).HasConversion(new JsonNodeConverter()).IsRequired(false);
        builder.Property(x => x.ResponsePayload).HasConversion(new JsonNodeConverter()).IsRequired(false);
        builder.Property(x => x.ErrorMessage).HasMaxLength(4000).IsRequired(false);
        builder.Property(x => x.Metadata).HasConversion(new JsonNodeDictionaryConverter(), new JsonNodeDictionaryComparer()).IsRequired(false);
        builder.HasIndex(x => x.OrchestrationInstanceId);
        builder
            .HasIndex(x => new { x.OrchestrationInstanceId, x.SourceTaskExecutionId, x.CompensationTaskKey })
            .IsUnique()
            .HasDatabaseName("IX_CompensationExecutions_Instance_SourceTask_CompensationTask");
    }

    private static void ConfigureIngressConfigurations(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<RuntimeIngressConfiguration>();
        builder.ToTable("RuntimeIngressConfigurations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.RuntimeOrchestrationArtifactId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.ConfigurationKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.IngressKind).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.MessageDisposition).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.IngressTransport).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.SettingsPayload).IsRequired();
        builder.Property(x => x.RouteTopic).HasMaxLength(512).IsRequired(false);
        builder.Property(x => x.RouteVersion).HasMaxLength(64).IsRequired(false);
        builder.HasIndex(x => new { x.RouteTopic, x.RouteVersion, x.IsActive });
        builder.HasIndex(x => new { x.RuntimeOrchestrationArtifactId, x.ConfigurationKey }).IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.IngressTransport, x.IngressKind });
        builder.HasIndex(x => new { x.IsActive, x.RuntimeOrchestrationArtifactId });
    }

    private static void ConfigureRuntimeExtensionPackages(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<RuntimeExtensionPackage>();
        builder.ToTable("RuntimeExtensionPackages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.BundleId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ExtensionKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(64).HasConversion(new SemanticVersionConverter()).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(256).IsRequired();
        builder.Property(x => x.SizeBytes).IsRequired();
        builder.Property(x => x.Manifest).HasConversion(new RuntimeExtensionManifestConverter()).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.StatusReason).HasMaxLength(4000).IsRequired(false);
        builder.HasIndex(x => new { x.ExtensionKey, x.Version, x.Status });
        builder.HasIndex(x => new { x.BundleId, x.ExtensionKey, x.Version }).IsUnique();
    }
}
