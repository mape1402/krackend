using System.Reflection;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Replication;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mule;
using Mule.Dispatching;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class RuntimeEntityFrameworkSchedulerTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ProjectionSchedulerCreatesDurableActionAndNotifiesMule()
    {
        var commitNotifier = Substitute.For<IMuleCommitNotifier>();
        await using var provider = CreateProvider(commitNotifier);
        using var scope = provider.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactProjectionScheduler>();

        var request = ProjectionRequest("artifact-1", 7);
        await scheduler.ScheduleProjectionAsync(request);

        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var action = Assert.Single(await dbContext.Set<DurableAction>().ToArrayAsync());
        var payload = JsonSerializer.Deserialize<RuntimeArtifactProjectionRequest>(action.Payload, SerializerOptions)!;

        Assert.Equal(ActionKey.From(RuntimeArtifactActionNames.ProjectIngressConfigurations), action.Key);
        Assert.Equal(RuntimeArtifactProjectionSchedulerDefaults.Lane, action.Lane);
        Assert.Equal("artifact-1:7", action.DeduplicationKey);
        Assert.Equal("artifact-1", action.CorrelationId);
        Assert.Equal(DurableActionStatus.Pending, action.Status);
        Assert.Equal(typeof(RuntimeArtifactProjectionRequest).AssemblyQualifiedName, action.PayloadType);
        Assert.Equal(request.ArtifactId, payload.ArtifactId);
        Assert.Equal(request.IngressGeneration, payload.IngressGeneration);
        await commitNotifier.Received(1).NotifySavedAsync(action.Id, action.Lane, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProjectionSchedulerDoesNotDuplicateExistingPendingAction()
    {
        var commitNotifier = Substitute.For<IMuleCommitNotifier>();
        await using var provider = CreateProvider(commitNotifier);
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var existing = DurableProjectionAction("artifact-dup", 3, DurableActionStatus.Pending);
        dbContext.Set<DurableAction>().Add(existing);
        await dbContext.SaveChangesAsync();

        var scheduler = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactProjectionScheduler>();
        await scheduler.ScheduleProjectionAsync(ProjectionRequest("artifact-dup", 3));

        Assert.Single(await dbContext.Set<DurableAction>().ToArrayAsync());
        await commitNotifier.DidNotReceiveWithAnyArgs().NotifySavedAsync(default, default!, default);
    }

    [Fact]
    public async Task ProjectionSchedulerReactivatesExistingFailedAction()
    {
        var commitNotifier = Substitute.For<IMuleCommitNotifier>();
        await using var provider = CreateProvider(commitNotifier);
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var existing = DurableProjectionAction("artifact-failed", 4, DurableActionStatus.Failed);
        existing.Attempts = 9;
        existing.LastError = "previous failure";
        existing.LockedOnUtc = DateTimeOffset.UtcNow;
        existing.StartedOnUtc = DateTimeOffset.UtcNow;
        existing.NextAttemptOnUtc = DateTimeOffset.UtcNow.AddMinutes(5);
        existing.CompletedOnUtc = DateTimeOffset.UtcNow;
        existing.TerminalOnUtc = DateTimeOffset.UtcNow;
        dbContext.Set<DurableAction>().Add(existing);
        await dbContext.SaveChangesAsync();

        var scheduler = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactProjectionScheduler>();
        await scheduler.ScheduleProjectionAsync(ProjectionRequest("artifact-failed", 4));

        var action = Assert.Single(await dbContext.Set<DurableAction>().ToArrayAsync());
        Assert.Equal(DurableActionStatus.Pending, action.Status);
        Assert.Equal(0, action.Attempts);
        Assert.Null(action.LastError);
        Assert.Null(action.LockedOnUtc);
        Assert.Null(action.StartedOnUtc);
        Assert.Null(action.NextAttemptOnUtc);
        Assert.Null(action.CompletedOnUtc);
        Assert.Null(action.TerminalOnUtc);
        await commitNotifier.Received(1).NotifySavedAsync(existing.Id, existing.Lane, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StandupSchedulerCreatesReplicaScopedActionAndMetadata()
    {
        var commitNotifier = Substitute.For<IMuleCommitNotifier>();
        await using var provider = CreateProvider(
            commitNotifier,
            new TestRuntimeReplicaIdentity
            {
                ReplicaId = "replica-x",
                ReplicaBootId = "boot-y",
                LocalStandupLane = "runtime-standup:replica-x"
            });
        using var scope = provider.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<IRuntimeIngressStandupScheduler>();

        var request = StandupRequest("artifact-standup", 12, "artifact-ready");
        await scheduler.ScheduleStandupAsync(request);

        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var action = Assert.Single(await dbContext.Set<DurableAction>().ToArrayAsync());
        var payload = JsonSerializer.Deserialize<RuntimeIngressStandupRequest>(action.Payload, SerializerOptions)!;
        var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(action.Metadata!, SerializerOptions)!;

        Assert.Equal(ActionKey.From(RuntimeArtifactActionNames.StandUpArtifactIngress), action.Key);
        Assert.Equal("runtime-standup:replica-x", action.Lane);
        Assert.Equal("artifact-standup:12:boot-y", action.DeduplicationKey);
        Assert.Equal("artifact-standup", action.CorrelationId);
        Assert.Equal(DurableActionStatus.Pending, action.Status);
        Assert.Equal(typeof(RuntimeIngressStandupRequest).AssemblyQualifiedName, action.PayloadType);
        Assert.Equal(request.ArtifactId, payload.ArtifactId);
        Assert.Equal(request.IngressGeneration, payload.IngressGeneration);
        Assert.Equal("replica-x", metadata["replicaId"]);
        Assert.Equal("boot-y", metadata["replicaBootId"]);
        Assert.Equal("artifact-ready", metadata["reason"]);
        Assert.Equal("12", metadata["ingressGeneration"]);
        await commitNotifier.Received(1).NotifySavedAsync(action.Id, action.Lane, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StandupSchedulerAllowsSameArtifactGenerationPerReplicaBoot()
    {
        var commitNotifier = Substitute.For<IMuleCommitNotifier>();
        await using var provider = CreateProvider(
            commitNotifier,
            new TestRuntimeReplicaIdentity
            {
                ReplicaId = "replica-a",
                ReplicaBootId = "boot-a",
                LocalStandupLane = "runtime-standup:replica-a"
            });
        using var scope = provider.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<IRuntimeIngressStandupScheduler>();

        await scheduler.ScheduleStandupAsync(StandupRequest("artifact-shared", 5, "initial"));
        var replica = scope.ServiceProvider.GetRequiredService<TestRuntimeReplicaIdentity>();
        replica.ReplicaBootId = "boot-b";
        await scheduler.ScheduleStandupAsync(StandupRequest("artifact-shared", 5, "restart"));

        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var actions = await dbContext.Set<DurableAction>().OrderBy(x => x.DeduplicationKey).ToArrayAsync();

        Assert.Equal(["artifact-shared:5:boot-a", "artifact-shared:5:boot-b"], actions.Select(x => x.DeduplicationKey).ToArray());
        await commitNotifier.Received(2).NotifySavedAsync(Arg.Any<Guid>(), "runtime-standup:replica-a", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StandupSchedulerDoesNotDuplicateExistingPendingAction()
    {
        var commitNotifier = Substitute.For<IMuleCommitNotifier>();
        await using var provider = CreateProvider(
            commitNotifier,
            new TestRuntimeReplicaIdentity
            {
                ReplicaId = "replica-a",
                ReplicaBootId = "boot-a",
                LocalStandupLane = "runtime-standup:replica-a"
            });
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var existing = DurableStandupAction("artifact-standup-dup", 8, "boot-a", DurableActionStatus.Pending);
        dbContext.Set<DurableAction>().Add(existing);
        await dbContext.SaveChangesAsync();

        var scheduler = scope.ServiceProvider.GetRequiredService<IRuntimeIngressStandupScheduler>();
        await scheduler.ScheduleStandupAsync(StandupRequest("artifact-standup-dup", 8, "duplicate"));

        Assert.Single(await dbContext.Set<DurableAction>().ToArrayAsync());
        await commitNotifier.DidNotReceiveWithAnyArgs().NotifySavedAsync(default, default!, default);
    }

    [Fact]
    public async Task StandupSchedulerReactivatesExistingFailedAction()
    {
        var commitNotifier = Substitute.For<IMuleCommitNotifier>();
        await using var provider = CreateProvider(
            commitNotifier,
            new TestRuntimeReplicaIdentity
            {
                ReplicaId = "replica-a",
                ReplicaBootId = "boot-a",
                LocalStandupLane = "runtime-standup:replica-a"
            });
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var existing = DurableStandupAction("artifact-standup-failed", 9, "boot-a", DurableActionStatus.Failed);
        existing.Attempts = 6;
        existing.LastError = "missing connector";
        existing.LockedOnUtc = DateTimeOffset.UtcNow;
        existing.StartedOnUtc = DateTimeOffset.UtcNow;
        existing.NextAttemptOnUtc = DateTimeOffset.UtcNow.AddMinutes(1);
        existing.CompletedOnUtc = DateTimeOffset.UtcNow;
        existing.TerminalOnUtc = DateTimeOffset.UtcNow;
        dbContext.Set<DurableAction>().Add(existing);
        await dbContext.SaveChangesAsync();

        var scheduler = scope.ServiceProvider.GetRequiredService<IRuntimeIngressStandupScheduler>();
        await scheduler.ScheduleStandupAsync(StandupRequest("artifact-standup-failed", 9, "reactivate"));

        var action = Assert.Single(await dbContext.Set<DurableAction>().ToArrayAsync());
        Assert.Equal(DurableActionStatus.Pending, action.Status);
        Assert.Equal(0, action.Attempts);
        Assert.Null(action.LastError);
        Assert.Null(action.LockedOnUtc);
        Assert.Null(action.StartedOnUtc);
        Assert.Null(action.NextAttemptOnUtc);
        Assert.Null(action.CompletedOnUtc);
        Assert.Null(action.TerminalOnUtc);
        await commitNotifier.Received(1).NotifySavedAsync(existing.Id, existing.Lane, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SchedulerDefersSaveAndNotificationWhenUnitOfWorkAutoSaveIsDisabled()
    {
        var commitNotifier = Substitute.For<IMuleCommitNotifier>();
        await using var provider = CreateProvider(
            commitNotifier,
            unitOfWorkFactory: services => new DeferredRuntimeStorageUnitOfWork(services.GetRequiredService<RuntimeDbContext>()));
        using var scope = provider.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactProjectionScheduler>();

        await scheduler.ScheduleProjectionAsync(ProjectionRequest("artifact-deferred", 1));

        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var pendingEntry = Assert.Single(dbContext.ChangeTracker.Entries<DurableAction>());
        Assert.Equal(EntityState.Added, pendingEntry.State);
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IRuntimeStorageUnitOfWork>();
        var deferScope = unitOfWork.DeferAutoSave();
        deferScope.Dispose();
        deferScope.Dispose();
        await commitNotifier.DidNotReceiveWithAnyArgs().NotifySavedAsync(default, default!, default);
    }

    [Fact]
    public async Task StandupSchedulerDefersSaveAndNotificationWhenUnitOfWorkAutoSaveIsDisabled()
    {
        var commitNotifier = Substitute.For<IMuleCommitNotifier>();
        await using var provider = CreateProvider(
            commitNotifier,
            unitOfWorkFactory: services => new DeferredRuntimeStorageUnitOfWork(services.GetRequiredService<RuntimeDbContext>()));
        using var scope = provider.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<IRuntimeIngressStandupScheduler>();

        await scheduler.ScheduleStandupAsync(StandupRequest("artifact-standup-deferred", 2, "deferred"));

        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var pendingEntry = Assert.Single(dbContext.ChangeTracker.Entries<DurableAction>());
        Assert.Equal(EntityState.Added, pendingEntry.State);
        Assert.Equal(ActionKey.From(RuntimeArtifactActionNames.StandUpArtifactIngress), pendingEntry.Entity.Key);
        await commitNotifier.DidNotReceiveWithAnyArgs().NotifySavedAsync(default, default!, default);
    }

    [Fact]
    public async Task ProjectionScheduler_WhenUniqueConstraintCollisionFindsExistingAction_NotifiesExistingAction()
    {
        var commitNotifier = Substitute.For<IMuleCommitNotifier>();
        await using var provider = CreateProvider(
            commitNotifier,
            unitOfWorkFactory: services => new ThrowingRuntimeStorageUnitOfWork(
                services.GetRequiredService<RuntimeDbContext>(),
                UniqueConstraintUpdateException()));
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var existing = DurableProjectionAction("artifact-concurrent", 10, DurableActionStatus.Pending);
        dbContext.Set<DurableAction>().Add(existing);
        await dbContext.SaveChangesAsync();
        var pending = DurableProjectionAction("artifact-concurrent", 10, DurableActionStatus.Pending);
        dbContext.Set<DurableAction>().Add(pending);
        var scheduler = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactProjectionScheduler>();

        await InvokeSaveAndNotifyAsync(
            scheduler,
            pending.Id,
            existing.Key,
            existing.DeduplicationKey,
            existing.Lane);

        Assert.DoesNotContain(
            dbContext.ChangeTracker.Entries<DurableAction>(),
            entry => entry.Entity.Id == pending.Id && entry.State == EntityState.Added);
        await commitNotifier.Received(1).NotifySavedAsync(existing.Id, existing.Lane, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProjectionScheduler_WhenUniqueConstraintCollisionCannotFindExistingAction_Rethrows()
    {
        var commitNotifier = Substitute.For<IMuleCommitNotifier>();
        var updateException = UniqueConstraintUpdateException();
        await using var provider = CreateProvider(
            commitNotifier,
            unitOfWorkFactory: services => new ThrowingRuntimeStorageUnitOfWork(
                services.GetRequiredService<RuntimeDbContext>(),
                updateException));
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var pending = DurableProjectionAction("artifact-missing-existing", 11, DurableActionStatus.Pending);
        dbContext.Set<DurableAction>().Add(pending);
        var scheduler = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactProjectionScheduler>();

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => InvokeSaveAndNotifyAsync(
            scheduler,
            pending.Id,
            pending.Key,
            pending.DeduplicationKey,
            pending.Lane));

        Assert.Same(updateException, exception);
        await commitNotifier.DidNotReceiveWithAnyArgs().NotifySavedAsync(default, default!, default);
    }

    [Fact]
    public async Task StandupScheduler_WhenUniqueConstraintCollisionFindsExistingAction_NotifiesExistingAction()
    {
        var commitNotifier = Substitute.For<IMuleCommitNotifier>();
        await using var provider = CreateProvider(
            commitNotifier,
            new TestRuntimeReplicaIdentity
            {
                ReplicaId = "replica-a",
                ReplicaBootId = "boot-a",
                LocalStandupLane = "runtime-standup:replica-a"
            },
            services => new ThrowingRuntimeStorageUnitOfWork(
                services.GetRequiredService<RuntimeDbContext>(),
                UniqueConstraintUpdateException()));
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var existing = DurableStandupAction("artifact-standup-concurrent", 12, "boot-a", DurableActionStatus.Pending);
        dbContext.Set<DurableAction>().Add(existing);
        await dbContext.SaveChangesAsync();
        var pending = DurableStandupAction("artifact-standup-concurrent", 12, "boot-a", DurableActionStatus.Pending);
        dbContext.Set<DurableAction>().Add(pending);
        var scheduler = scope.ServiceProvider.GetRequiredService<IRuntimeIngressStandupScheduler>();

        await InvokeSaveAndNotifyAsync(
            scheduler,
            pending.Id,
            existing.Key,
            existing.DeduplicationKey,
            existing.Lane);

        Assert.DoesNotContain(
            dbContext.ChangeTracker.Entries<DurableAction>(),
            entry => entry.Entity.Id == pending.Id && entry.State == EntityState.Added);
        await commitNotifier.Received(1).NotifySavedAsync(existing.Id, existing.Lane, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StandupScheduler_WhenUniqueConstraintCollisionCannotFindExistingAction_Rethrows()
    {
        var commitNotifier = Substitute.For<IMuleCommitNotifier>();
        var updateException = UniqueConstraintUpdateException();
        await using var provider = CreateProvider(
            commitNotifier,
            new TestRuntimeReplicaIdentity
            {
                ReplicaId = "replica-a",
                ReplicaBootId = "boot-a",
                LocalStandupLane = "runtime-standup:replica-a"
            },
            services => new ThrowingRuntimeStorageUnitOfWork(
                services.GetRequiredService<RuntimeDbContext>(),
                updateException));
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var pending = DurableStandupAction("artifact-standup-missing-existing", 13, "boot-a", DurableActionStatus.Pending);
        dbContext.Set<DurableAction>().Add(pending);
        var scheduler = scope.ServiceProvider.GetRequiredService<IRuntimeIngressStandupScheduler>();

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => InvokeSaveAndNotifyAsync(
            scheduler,
            pending.Id,
            pending.Key,
            pending.DeduplicationKey,
            pending.Lane));

        Assert.Same(updateException, exception);
        await commitNotifier.DidNotReceiveWithAnyArgs().NotifySavedAsync(default, default!, default);
    }

    private static ServiceProvider CreateProvider(
        IMuleCommitNotifier commitNotifier,
        TestRuntimeReplicaIdentity? replicaIdentity = null,
        Func<IServiceProvider, IRuntimeStorageUnitOfWork>? unitOfWorkFactory = null)
    {
        var services = new ServiceCollection();
        var replica = replicaIdentity ?? new TestRuntimeReplicaIdentity();

        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.AddOrchestratorRuntimeStorageEntityFramework(options =>
            options.UseInMemoryDatabase($"runtime-scheduler-{Guid.NewGuid():N}"));
        services.Replace(ServiceDescriptor.Singleton<IRuntimeReplicaIdentity>(replica));
        services.AddSingleton(replica);
        services.Replace(ServiceDescriptor.Scoped<IMuleCommitNotifier>(_ => commitNotifier));

        if (unitOfWorkFactory is not null)
        {
            services.Replace(ServiceDescriptor.Scoped<IRuntimeStorageUnitOfWork>(unitOfWorkFactory));
        }

        return services.BuildServiceProvider();
    }

    private static Task InvokeSaveAndNotifyAsync(
        object scheduler,
        Guid actionId,
        ActionKey key,
        string deduplicationKey,
        string lane)
    {
        var method = scheduler.GetType().GetMethod(
            "SaveAndNotifyIfNeededAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        return (Task)method.Invoke(
            scheduler,
            [actionId, key, deduplicationKey, lane, CancellationToken.None])!;
    }

    private static DbUpdateException UniqueConstraintUpdateException()
        => new("Unique key collision.", CreateSqlException(2627));

    private static SqlException CreateSqlException(int number)
    {
        var errorConstructor = typeof(SqlError)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .OrderByDescending(constructor => constructor.GetParameters().Length)
            .First();
        var errorArguments = errorConstructor
            .GetParameters()
            .Select(parameter => CreateSqlErrorArgument(parameter, number))
            .ToArray();
        var error = (SqlError)errorConstructor.Invoke(errorArguments);

        var collectionConstructor = typeof(SqlErrorCollection).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            Type.EmptyTypes,
            modifiers: null);
        var collection = (SqlErrorCollection)collectionConstructor!.Invoke([]);
        typeof(SqlErrorCollection)
            .GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(collection, [error]);

        return (SqlException)typeof(SqlException)
            .GetMethod(
                "CreateException",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                [typeof(SqlErrorCollection), typeof(string)],
                modifiers: null)!
            .Invoke(null, [collection, "11.0.0"])!;
    }

    private static object? CreateSqlErrorArgument(ParameterInfo parameter, int number)
    {
        if (parameter.ParameterType == typeof(int))
        {
            return parameter.Name?.Contains("number", StringComparison.OrdinalIgnoreCase) == true
                ? number
                : 0;
        }

        if (parameter.ParameterType == typeof(byte))
        {
            return (byte)1;
        }

        if (parameter.ParameterType == typeof(string))
        {
            return parameter.Name?.Contains("message", StringComparison.OrdinalIgnoreCase) == true
                ? "Unique constraint violation."
                : parameter.Name ?? string.Empty;
        }

        if (parameter.ParameterType == typeof(Exception))
        {
            return null;
        }

        return parameter.HasDefaultValue ? parameter.DefaultValue : null;
    }

    private static RuntimeArtifactProjectionRequest ProjectionRequest(string artifactId, long generation)
        => new()
        {
            ArtifactId = artifactId,
            IngressGeneration = generation,
            RequestedBy = "tests",
            RequestedOnUtc = DateTime.UtcNow
        };

    private static RuntimeIngressStandupRequest StandupRequest(string artifactId, long generation, string reason)
        => new()
        {
            ArtifactId = artifactId,
            IngressGeneration = generation,
            Reason = reason,
            RequestedOnUtc = DateTime.UtcNow
        };

    private static DurableAction DurableProjectionAction(
        string artifactId,
        long generation,
        DurableActionStatus status)
        => new()
        {
            Id = Guid.NewGuid(),
            Key = ActionKey.From(RuntimeArtifactActionNames.ProjectIngressConfigurations),
            Lane = RuntimeArtifactProjectionSchedulerDefaults.Lane,
            Payload = JsonSerializer.Serialize(ProjectionRequest(artifactId, generation), SerializerOptions),
            PayloadType = typeof(RuntimeArtifactProjectionRequest).AssemblyQualifiedName!,
            CorrelationId = artifactId,
            DeduplicationKey = $"{artifactId}:{generation}",
            Status = status,
            CreatedOnUtc = DateTimeOffset.UtcNow
        };

    private static DurableAction DurableStandupAction(
        string artifactId,
        long generation,
        string replicaBootId,
        DurableActionStatus status)
        => new()
        {
            Id = Guid.NewGuid(),
            Key = ActionKey.From(RuntimeArtifactActionNames.StandUpArtifactIngress),
            Lane = "runtime-standup:replica-a",
            Payload = JsonSerializer.Serialize(StandupRequest(artifactId, generation, "existing"), SerializerOptions),
            PayloadType = typeof(RuntimeIngressStandupRequest).AssemblyQualifiedName!,
            Metadata = """{"replicaId":"replica-a","replicaBootId":"boot-a"}""",
            CorrelationId = artifactId,
            DeduplicationKey = $"{artifactId}:{generation}:{replicaBootId}",
            Status = status,
            CreatedOnUtc = DateTimeOffset.UtcNow
        };

    private sealed class DeferredRuntimeStorageUnitOfWork : IRuntimeStorageUnitOfWork
    {
        private readonly RuntimeDbContext _dbContext;

        public DeferredRuntimeStorageUnitOfWork(RuntimeDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public bool AutoSaveChanges => false;

        public Task<IRuntimeStorageTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IRuntimeStorageTransaction>(new NoopRuntimeStorageTransaction());

        public IDisposable DeferAutoSave()
            => new NoopDisposable();

        public Task SaveChanges(CancellationToken cancellationToken = default)
            => _dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed class ThrowingRuntimeStorageUnitOfWork : IRuntimeStorageUnitOfWork
    {
        private readonly RuntimeDbContext _dbContext;
        private readonly DbUpdateException _exception;

        public ThrowingRuntimeStorageUnitOfWork(RuntimeDbContext dbContext, DbUpdateException exception)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        public bool AutoSaveChanges => true;

        public Task<IRuntimeStorageTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IRuntimeStorageTransaction>(new NoopRuntimeStorageTransaction());

        public IDisposable DeferAutoSave()
            => new NoopDisposable();

        public Task SaveChanges(CancellationToken cancellationToken = default)
        {
            _ = _dbContext;
            throw _exception;
        }
    }
}
