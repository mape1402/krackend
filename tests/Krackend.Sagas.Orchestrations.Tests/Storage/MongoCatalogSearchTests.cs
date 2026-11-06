using DotNet.Testcontainers.Builders;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Mongo;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json.Nodes;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class MongoCatalogSearchTests
{
    // Keep Testcontainers deterministic: newer MongoDB 8.0 images deliberately
    // refuse the hosted Linux kernels used by GitHub Actions (SERVER-121912).
    private const string MongoImage = "mongo:8.0.4";

    [Fact]
    public async Task ArchivedBackchannelActivationCommitsOrRollsBackTogetherInMongoReplicaSet()
    {
        await using var mongo = new ContainerBuilder()
            .WithImage(MongoImage)
            .WithCommand("--replSet", "rs0", "--bind_ip_all")
            .WithPortBinding(27017, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(27017))
            .Build();
        await mongo.StartAsync();
        var initialized = await mongo.ExecAsync(["mongosh", "--quiet", "--eval",
            "rs.initiate({_id:'rs0',members:[{_id:0,host:'localhost:27017'}]}); for (let i=0; i<200 && !db.hello().isWritablePrimary; i++) sleep(100); if (!db.hello().isWritablePrimary) quit(2);"]);
        Assert.Equal(0, initialized.ExitCode);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.AddOrchestratorRuntimeStorageEntityFramework(options =>
            options.UseMongoDB($"mongodb://{mongo.Hostname}:{mongo.GetMappedPublicPort(27017)}/?replicaSet=rs0&directConnection=true", "archived_transaction_test"),
            storage => storage.ConfigureModel = model => model.UseKrackendMongoRuntimeModel());
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await db.Database.EnsureCreatedAsync();
        var artifacts = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>();
        var artifact = new RuntimeOrchestrationArtifact
        {
            Id = Id.New(), OrchestrationDefinitionKey = "archived.orders", SourceOrchestrationVersionId = Id.New(),
            ArtifactType = "orchestration-version-snapshot", Version = new SemanticVersion(1, 0, 0),
            ArtifactChecksum = new Checksum("archived-checksum"), ArtifactPayload = new JsonObject(),
            Status = RuntimeOrchestrationArtifactStatus.Retired, LifecycleStatus = RuntimeOrchestrationLifecycleStatus.Archived,
            IsActive = false, IngressGeneration = 3, DeployedOnUtc = DateTime.UtcNow
        };
        await artifacts.Upsert(artifact);
        var ingress = scope.ServiceProvider.GetRequiredService<IRuntimeIngressConfigurationRepository>();
        await ingress.UpsertForArtifactAsync(artifact.Id,
        [new RuntimeIngressConfiguration { Id = Id.New(), RuntimeOrchestrationArtifactId = artifact.Id,
            ConfigurationKey = "backchannel", IngressKind = IngressKind.Backchannel, SettingsPayload = "{}", IsActive = false }]);
        var activation = Assert.IsAssignableFrom<IArchivedRecoveryActivationRepository>(artifacts);
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IRuntimeStorageUnitOfWork>();

        await using (var transaction = await unitOfWork.BeginTransactionAsync())
        {
            using (unitOfWork.DeferAutoSave())
            {
                Assert.Equal(4, await activation.TryActivateArchivedBackchannelsAsync(artifact.Id, 3));
                await unitOfWork.SaveChanges();
            }
        }
        Assert.False((await artifacts.GetById(artifact.Id)).IsActive);
        Assert.False((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync()).IsActive);

        await using (var transaction = await unitOfWork.BeginTransactionAsync())
        {
            using (unitOfWork.DeferAutoSave())
            {
                Assert.Equal(4, await activation.TryActivateArchivedBackchannelsAsync(artifact.Id, 3));
                await unitOfWork.SaveChanges();
            }
            await transaction.CommitAsync();
        }
        Assert.True((await artifacts.GetById(artifact.Id)).IsActive);
        Assert.True((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync()).IsActive);

        var rollbackTarget = new RuntimeOrchestrationArtifact
        {
            Id = Id.New(), OrchestrationDefinitionKey = "rollback.orders", SourceOrchestrationVersionId = Id.New(),
            ArtifactType = "orchestration-version-snapshot", Version = new SemanticVersion(1, 0, 0),
            ArtifactChecksum = new Checksum("rollback-checksum"), ArtifactPayload = new JsonObject(),
            Status = RuntimeOrchestrationArtifactStatus.Ready, LifecycleStatus = RuntimeOrchestrationLifecycleStatus.Active,
            IsActive = true, IngressGeneration = 4, DeployedOnUtc = DateTime.UtcNow
        };
        await artifacts.Upsert(rollbackTarget);
        await VerifyFence(artifact.Id, rollback: false);
        await VerifyFence(rollbackTarget.Id, rollback: true);

        const string identity = "MESSAGING|ORDERS|1.0.0";
        var originalClaimTime = DateTime.UtcNow.AddMinutes(1);
        db.RuntimeTriggerClaims.Add(new RuntimeTriggerClaim
        {
            Id = identity, RuntimeOrchestrationArtifactId = rollbackTarget.Id, UpdatedOnUtc = originalClaimTime
        });
        await db.SaveChangesAsync();
        using (var ownerScope = provider.CreateScope())
        using (var contenderScope = provider.CreateScope())
        {
            var ownerUnit = ownerScope.ServiceProvider.GetRequiredService<IRuntimeStorageUnitOfWork>();
            var contenderUnit = contenderScope.ServiceProvider.GetRequiredService<IRuntimeStorageUnitOfWork>();
            var ownerClaims = ownerScope.ServiceProvider.GetRequiredService<IRuntimeTriggerClaimRepository>();
            var contenderClaims = contenderScope.ServiceProvider.GetRequiredService<IRuntimeTriggerClaimRepository>();
            await using var ownerTransaction = await ownerUnit.BeginTransactionAsync();
            Assert.True((await ownerClaims.ClaimAsync([identity], rollbackTarget.Id, [])).Succeeded);
            var touched = await ownerScope.ServiceProvider.GetRequiredService<RuntimeDbContext>().RuntimeTriggerClaims.AsNoTracking()
                .SingleAsync(x => x.Id == identity);
            Assert.True(touched.UpdatedOnUtc > originalClaimTime);
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var attempt = Task.Run(async () =>
            {
                await using var transaction = await contenderUnit.BeginTransactionAsync();
                started.SetResult();
                var result = await contenderClaims.ClaimAsync([identity], Id.New(), [rollbackTarget.Id]);
                await transaction.CommitAsync();
                return result.Succeeded;
            });
            await started.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await Task.WhenAny(attempt, Task.Delay(1000));
            var replacedBeforeOwnerCommitted = attempt.IsCompletedSuccessfully && await attempt;
            await ownerTransaction.CommitAsync();
            try { await attempt.WaitAsync(TimeSpan.FromSeconds(20)); }
            catch (Exception exception) when (exception is DbUpdateException or MongoException) { /* A write conflict is safe. */ }
            Assert.False(replacedBeforeOwnerCommitted);
        }

        db.ChangeTracker.Clear();
        var retirement = Assert.IsAssignableFrom<IArchivedRecoveryRetirementRepository>(artifacts);
        var batchRetirement = Assert.IsAssignableFrom<IArchivedRecoveryBatchRepository>(artifacts);
        Assert.Equal(artifact.Id, Assert.Single(await retirement.GetArchivedRecoveryCandidates(0, 10)).ArtifactId);
        var lifecycleFence = Assert.IsAssignableFrom<IRuntimeArtifactLifecycleFenceRepository>(artifacts);
        await using (var transaction = await unitOfWork.BeginTransactionAsync())
        {
            using (unitOfWork.DeferAutoSave())
            {
                Assert.True(await lifecycleFence.TryFenceLifecycle(artifact.Id, 5));
                await unitOfWork.SaveChanges();
                Assert.Contains(artifact.Id, await batchRetirement.RetireArchivedRecoveries([new ArchivedRecoveryCandidate(artifact.Id, 6)]));
                await unitOfWork.SaveChanges();
            }
        }
        Assert.True((await artifacts.GetById(artifact.Id)).IsActive);
        Assert.True((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync()).IsActive);
        var instances = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var instance = new OrchestrationInstance
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = artifact.Id, TriggerIntakeId = Id.New(),
            OrchestrationDefinitionKey = "archived.orders", CorrelationId = "corr", SagaId = "saga", ExecutionKey = "archived:1",
            StartIdempotencyKey = "archived:1:start", Status = OrchestrationInstanceStatus.DeadLettered,
            StartedOnUtc = DateTime.UtcNow.AddDays(-2), LastUpdatedOnUtc = DateTime.UtcNow.AddDays(-2)
        };
        await instances.Create(instance);
        Assert.NotNull(await instances.TryAcquireLease(instance.Id, "recovery-test", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1)));
        var registry = Substitute.For<IIngressRegistry, IArchivedRecoveryIngressCleanup>();
        var cleanup = (IArchivedRecoveryIngressCleanup)registry;
        cleanup.GetLocalArtifactIds().Returns([artifact.Id.ToString()]);
        cleanup.ShutDownIfArchivedAsync(artifact.Id.ToString(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        var processor = new ArchivedRecoveryRetirementProcessor(artifacts, instances, unitOfWork, registry,
            Options.Create(new OrchestrationTimeoutOptions()), Options.Create(new OrchestrationCoordinationOptions()),
            NullLogger<ArchivedRecoveryRetirementProcessor>.Instance);
        Assert.Equal(0, await processor.ProcessAsync());
        Assert.True((await artifacts.GetById(artifact.Id)).IsActive);
        await instances.ReleaseLease(instance.Id, "recovery-test");
        Assert.Equal(1, await processor.ProcessAsync());
        Assert.False((await artifacts.GetById(artifact.Id)).IsActive);
        Assert.False((await db.RuntimeIngressConfigurations.AsNoTracking().SingleAsync()).IsActive);
        Assert.Contains(artifact.Id, await retirement.GetRetiredArchivedIds([artifact.Id]));
        Assert.Empty(await retirement.GetArchivedRecoveryCandidates(0, 10));
        await cleanup.Received(1).ShutDownIfArchivedAsync(artifact.Id.ToString(), Arg.Any<CancellationToken>());

        async Task VerifyFence(Id artifactId, bool rollback)
        {
            using var firstScope = provider.CreateScope();
            using var secondScope = provider.CreateScope();
            var firstArtifacts = firstScope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>();
            var secondArtifacts = secondScope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>();
            var firstUnit = firstScope.ServiceProvider.GetRequiredService<IRuntimeStorageUnitOfWork>();
            var secondUnit = secondScope.ServiceProvider.GetRequiredService<IRuntimeStorageUnitOfWork>();
            Task<bool> Fence(IRuntimeArtifactRepository repository) => rollback
                ? ((IRuntimeArtifactRolloutTransitionRepository)repository).TryFenceRollbackTarget(artifactId, 4)
                : ((IRuntimeArtifactLifecycleFenceRepository)repository).TryFenceLifecycle(artifactId, 4);
            await using var firstTransaction = await firstUnit.BeginTransactionAsync();
            using (firstUnit.DeferAutoSave())
            {
                Assert.True(await Fence(firstArtifacts));
                await firstUnit.SaveChanges();
            }
            var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondTransaction = Task.Run(async () =>
            {
                await using var transaction = await secondUnit.BeginTransactionAsync();
                bool acquired;
                using (secondUnit.DeferAutoSave())
                {
                    acquired = await Fence(secondArtifacts);
                    secondStarted.SetResult();
                    if (acquired) await secondUnit.SaveChanges();
                }
                await transaction.CommitAsync();
                return acquired;
            });
            await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await Task.WhenAny(secondTransaction, Task.Delay(1000));
            var passedWhileFirstHeldFence = secondTransaction.IsCompletedSuccessfully && await secondTransaction;
            await firstTransaction.CommitAsync();
            try { await secondTransaction.WaitAsync(TimeSpan.FromSeconds(20)); }
            catch (Exception exception) when (exception is DbUpdateException or MongoException) { /* A write conflict is a safe outcome. */ }
            Assert.False(passedWhileFirstHeldFence);
            using var verificationScope = provider.CreateScope();
            var persisted = await verificationScope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>().GetById(artifactId);
            Assert.Equal(5, persisted.IngressGeneration);
        }
    }

    [Fact]
    public async Task RuntimeInstancePageLoadsProjectedProgressInMongo()
    {
        await using var mongo = new ContainerBuilder()
            .WithImage(MongoImage)
            .WithPortBinding(27017, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(27017))
            .Build();
        await mongo.StartAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.AddOrchestratorRuntimeStorageEntityFramework(options =>
            options.UseMongoDB($"mongodb://{mongo.Hostname}:{mongo.GetMappedPublicPort(27017)}", "instance_grid_test"),
            storage => storage.ConfigureModel = model =>
            {
                model.UseKrackendMongoRuntimeModel();
                model.Entity<OrchestrationInstance>().HasIndex(x => x.RetryCount, "IX_Test_RetryCount");
            });
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        db.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        await db.Database.EnsureCreatedAsync();
        await db.Database.EnsureCreatedAsync();
        var database = new MongoClient($"mongodb://{mongo.Hostname}:{mongo.GetMappedPublicPort(27017)}")
            .GetDatabase("instance_grid_test");
        var collection = database.GetCollection<BsonDocument>("OrchestrationInstances");
        using (var indexes = await collection.Indexes.ListAsync())
        {
            var initialIndexes = await indexes.ToListAsync();
            Assert.Contains(initialIndexes, index => index["key"].AsBsonDocument.Contains("StatisticsRevision"));
            Assert.Contains(initialIndexes, index => index["name"] == "IX_Test_RetryCount");
        }
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        await repository.Create(new OrchestrationInstance
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = Id.New(), TriggerIntakeId = Id.New(),
            OrchestrationDefinitionKey = "sales", CorrelationId = "corr", SagaId = "saga", ExecutionKey = "sales:1",
            OrchestrationVersion = "1.2.3", TotalTaskCount = 3, CompletedTaskCount = 1,
            StartedOnUtc = DateTime.UtcNow, LastUpdatedOnUtc = DateTime.UtcNow,
            SnapshotPayload = JsonNode.Parse("{\"large\":\"payload\"}")
        });

        var page = await ((IRuntimeInstanceCatalogRepository)repository).GetPage(new RuntimeInstanceCatalogQuery());

        var item = Assert.Single(page.Items);
        Assert.Equal("1.2.3", item.OrchestrationVersion);
        Assert.Equal(3, item.TotalTaskCount);
        Assert.Equal(1, item.CompletedTaskCount);
        Assert.Null(item.SnapshotPayload);

        var recent = await ((IRuntimeInstanceRecentSummaryReader)repository).GetRecentSummaries(10);
        var summary = Assert.Single(recent);
        Assert.Equal("1.2.3", summary.OrchestrationVersion);
        Assert.Equal(3, summary.TotalTaskCount);
        Assert.Equal(1, summary.CompletedTaskCount);
        Assert.Null(summary.SnapshotPayload);
        var traffic = await scope.ServiceProvider.GetRequiredService<IExecutionTransitionRepository>()
            .GetTraffic(DateTime.UtcNow.AddHours(-1));
        Assert.Contains(traffic, point => point.Started > 0);

        var lifetimeArtifactId = Id.New();
        var overdue = new OrchestrationInstance
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = lifetimeArtifactId, TriggerIntakeId = Id.New(),
            OrchestrationDefinitionKey = "sales", CorrelationId = "corr", SagaId = "saga", ExecutionKey = "sales:overdue",
            StartIdempotencyKey = "sales:overdue:start",
            Status = OrchestrationInstanceStatus.Running, StartedOnUtc = DateTime.UtcNow.AddDays(-2),
            LastUpdatedOnUtc = DateTime.UtcNow.AddDays(-2)
        };
        await repository.Create(overdue);
        var lifetimeCandidates = Assert.IsAssignableFrom<IOrchestrationLifetimeCandidateRepository>(repository);
        Assert.Equal(overdue.Id, Assert.Single(await lifetimeCandidates.GetLifetimeCandidates(lifetimeArtifactId, DateTime.UtcNow.AddDays(-1), 0, 100)));
        var globalCandidates = Assert.IsAssignableFrom<IOrchestrationLifetimeScanRepository>(repository);
        var globalCandidate = Assert.Single(await globalCandidates.GetLifetimeCandidatePage(DateTime.UtcNow.AddDays(-1), 0, 100));
        Assert.Equal(overdue.Id, globalCandidate.InstanceId);
        Assert.Equal(lifetimeArtifactId, globalCandidate.ArtifactId);
        Assert.True(await Assert.IsAssignableFrom<IOrchestrationArtifactInstanceReader>(repository).HasActiveInstances(lifetimeArtifactId));

        var leasedArtifactId = Id.New();
        var leasedInstance = new OrchestrationInstance
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = leasedArtifactId, TriggerIntakeId = Id.New(),
            OrchestrationDefinitionKey = "sales", CorrelationId = "corr-leased", SagaId = "saga-leased", ExecutionKey = "sales:leased",
            StartIdempotencyKey = "sales:leased:start", Status = OrchestrationInstanceStatus.DeadLettered,
            StartedOnUtc = DateTime.UtcNow.AddDays(-2), LastUpdatedOnUtc = DateTime.UtcNow.AddDays(-2),
            ActiveLeaseId = "recovery-test", ActiveLeaseExpiresOnUtc = DateTime.UtcNow.AddMinutes(1)
        };
        await repository.Create(leasedInstance);
        var activeInstances = Assert.IsAssignableFrom<IOrchestrationArtifactInstanceReader>(repository);
        Assert.True(await activeInstances.HasActiveInstances(leasedArtifactId));
        await repository.ReleaseLease(leasedInstance.Id, "recovery-test");
        Assert.False(await activeInstances.HasActiveInstances(leasedArtifactId));

        using var firstLeaseScope = provider.CreateScope();
        using var secondLeaseScope = provider.CreateScope();
        var firstLeaseDb = firstLeaseScope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var secondLeaseDb = secondLeaseScope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        firstLeaseDb.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        secondLeaseDb.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        await firstLeaseDb.OrchestrationInstances.SingleAsync(x => x.Id == leasedInstance.Id);
        await secondLeaseDb.OrchestrationInstances.SingleAsync(x => x.Id == leasedInstance.Id);
        var firstLeaseRepository = firstLeaseScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var secondLeaseRepository = secondLeaseScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var now = DateTime.UtcNow;
        Assert.NotNull(await firstLeaseRepository.TryAcquireLease(leasedInstance.Id, "first-owner", now, now.AddMinutes(1)));
        Assert.Null(await secondLeaseRepository.TryAcquireLease(leasedInstance.Id, "second-owner", now, now.AddMinutes(1)));
        Assert.NotNull(await firstLeaseRepository.TryAcquireLease(leasedInstance.Id, "first-owner", now, now.AddMinutes(1)));
        await secondLeaseRepository.ReleaseLease(leasedInstance.Id, "second-owner");
        Assert.Equal("first-owner", (await secondLeaseRepository.GetById(leasedInstance.Id)).ActiveLeaseId);
        await firstLeaseRepository.ReleaseLease(leasedInstance.Id, "first-owner");
        Assert.Null((await firstLeaseRepository.GetById(leasedInstance.Id)).ActiveLeaseId);

        var concurrent = Enumerable.Range(0, 8).Select(async index =>
        {
            using var contenderScope = provider.CreateScope();
            contenderScope.ServiceProvider.GetRequiredService<RuntimeDbContext>().Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
            return await contenderScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>()
                .TryAcquireLease(leasedInstance.Id, $"contender-{index}", now, now.AddMinutes(1));
        }).ToArray();
        var winner = Assert.Single((await Task.WhenAll(concurrent)).OfType<OrchestrationInstanceLease>());
        var takeover = await secondLeaseRepository.TryAcquireLease(leasedInstance.Id, "after-expiry", now.AddMinutes(2), now.AddMinutes(3));
        Assert.NotNull(takeover);
        await firstLeaseRepository.ReleaseLease(leasedInstance.Id, winner.LeaseId);
        Assert.Equal("after-expiry", (await firstLeaseRepository.GetById(leasedInstance.Id)).ActiveLeaseId);

        // EnsureCreated adds new model indexes without removing existing ones.
        var updatedServices = new ServiceCollection();
        updatedServices.AddLogging();
        updatedServices.AddKrackendOrchestrationsRuntime();
        updatedServices.AddOrchestratorRuntimeStorageEntityFramework(options =>
            options.UseMongoDB($"mongodb://{mongo.Hostname}:{mongo.GetMappedPublicPort(27017)}", "instance_grid_test"),
            storage => storage.ConfigureModel = model =>
            {
                model.UseKrackendMongoRuntimeModel();
                model.Entity<OrchestrationInstance>().HasIndex(x => x.RetryCount, "IX_Test_RetryCount");
                model.Entity<OrchestrationInstance>().HasIndex(x => new { x.RetryCount, x.StartedOnUtc }, "IX_Test_ChangedRetryCount");
            });
        await using var updatedProvider = updatedServices.BuildServiceProvider();
        using var updatedScope = updatedProvider.CreateScope();
        await updatedScope.ServiceProvider.GetRequiredService<RuntimeDbContext>().Database.EnsureCreatedAsync();
        using var updatedIndexes = await collection.Indexes.ListAsync();
        var existingIndexes = await updatedIndexes.ToListAsync();
        Assert.Contains(existingIndexes, index => index["name"] == "IX_Test_RetryCount");
        Assert.Contains(existingIndexes, index => index["name"] == "IX_Test_ChangedRetryCount");
    }

    [Fact]
    public async Task RuntimeArtifactGroupsAreFilteredAndPagedInMongo()
    {
        await using var mongo = new ContainerBuilder()
            .WithImage(MongoImage)
            .WithPortBinding(27017, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(27017))
            .Build();
        await mongo.StartAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.AddOrchestratorRuntimeStorageEntityFramework(options =>
            options.UseMongoDB($"mongodb://{mongo.Hostname}:{mongo.GetMappedPublicPort(27017)}", "runtime_catalog_tests"),
            storage => storage.ConfigureModel = model => model.UseKrackendMongoRuntimeModel());
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        await context.Database.EnsureCreatedAsync();
        var artifacts = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>();
        var catalog = Assert.IsAssignableFrom<IRuntimeArtifactUiCatalog>(artifacts);
        foreach (var (key, version, active) in new[] { ("sales.created", 1, true), ("sales.created", 2, false), ("billing.created", 1, true) })
            await artifacts.Upsert(new RuntimeOrchestrationArtifact
            {
                Id = Id.New(), OrchestrationDefinitionKey = key, SourceOrchestrationVersionId = Id.New(),
                ArtifactType = "orchestration-version-snapshot", Version = new SemanticVersion(version, 0, 0),
                ArtifactChecksum = new Checksum($"checksum-{key}-{version}"), ArtifactPayload = JsonNode.Parse("{}")!,
                Status = RuntimeOrchestrationArtifactStatus.Ready, IsActive = active, DeployedOnUtc = DateTime.UtcNow,
                LastReleaseId = $"release-{version}", LastControlPlaneArtifactId = $"artifact-{version}"
            });
        var page = await catalog.GetGroups(1, 1, "sales");
        Assert.Equal(1, page.TotalCount);
        var group = Assert.Single(page.Items);
        Assert.Equal("sales.created", group.Key);
        Assert.Equal(2, group.VersionCount);
        Assert.Equal(1, group.ActiveCount);
        var groups = await catalog.GetGroups(1, 1, "");
        Assert.Equal(2, groups.TotalCount);
        Assert.Equal("billing.created", Assert.Single(groups.Items).Key);
        Assert.Equal("sales.created", Assert.Single((await catalog.GetGroups(2, 1, "")).Items).Key);
        Assert.Empty((await catalog.GetGroups(1, 1, "missing")).Items);
        var versions = await catalog.GetVersions("sales.created", 1, 1, "");
        Assert.Equal(2, versions.TotalCount);
        Assert.Single(versions.Items);
        Assert.Single((await catalog.GetVersions("sales.created", 1, 25, "1.0.0")).Items);
        Assert.Single((await catalog.GetVersions("sales.created", 1, 25, "release-2")).Items);
        var policies = Assert.IsAssignableFrom<IRuntimeArtifactLifetimePolicyRepository>(artifacts);
        Assert.Equal(2, (await policies.GetLifetimePolicyPage(0, 100)).Count);

        using var competingScope = provider.CreateScope();
        var competingContext = competingScope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        competingContext.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        const string identity = "MESSAGING|ORDERS|1.0.0";
        var previousOwner = Id.New();
        var firstOwner = Id.New();
        context.RuntimeTriggerClaims.Add(new RuntimeTriggerClaim
        {
            Id = identity, RuntimeOrchestrationArtifactId = previousOwner, UpdatedOnUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        await competingContext.RuntimeTriggerClaims.SingleAsync(x => x.Id == identity);
        var firstClaims = scope.ServiceProvider.GetRequiredService<IRuntimeTriggerClaimRepository>();
        var secondClaims = competingScope.ServiceProvider.GetRequiredService<IRuntimeTriggerClaimRepository>();
        Assert.True((await firstClaims.ClaimAsync([identity], firstOwner, [previousOwner])).Succeeded);
        Assert.False((await secondClaims.ClaimAsync([identity], Id.New(), [previousOwner])).Succeeded);
        Assert.Equal(firstOwner, (await competingContext.RuntimeTriggerClaims.AsNoTracking().SingleAsync(x => x.Id == identity)).RuntimeOrchestrationArtifactId);

        var ingress = scope.ServiceProvider.GetRequiredService<IRuntimeIngressConfigurationRepository>();
        const string previousKey = "trigger:previous";
        await ingress.UpsertForArtifactAsync(previousOwner, [new RuntimeIngressConfiguration
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = previousOwner, ConfigurationKey = previousKey,
            IngressKind = IngressKind.Trigger, SettingsPayload = "{}", IsActive = true
        }]);
        await RuntimeArtifactRolloutHandoff.ApplyAsync(new RuntimeArtifactProjectionRequest
        {
            ArtifactId = firstOwner.ToString(), RolloutTriggerIdentities = [identity],
            RolloutReplacements = [new() { ArtifactId = previousOwner.ToString(), ConfigurationKeys = [previousKey] }]
        }, ingress, firstClaims);
        Assert.False((await context.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == previousKey)).IsActive);

        var pending = new RuntimeOrchestrationArtifact
        {
            Id = Id.New(), OrchestrationDefinitionKey = "staged.orders", SourceOrchestrationVersionId = Id.New(),
            ArtifactType = "orchestration-version-snapshot", Version = new SemanticVersion(1, 0, 0),
            ArtifactChecksum = new Checksum("staged-checksum"), ArtifactPayload = new JsonObject(),
            Status = RuntimeOrchestrationArtifactStatus.Pending, IsActive = true, IngressGeneration = 3,
            DeployedOnUtc = DateTime.UtcNow
        };
        await artifacts.Upsert(pending);
        await ingress.UpsertForArtifactAsync(pending.Id, [new RuntimeIngressConfiguration
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = pending.Id, ConfigurationKey = "trigger:staged",
            IngressKind = IngressKind.Trigger, SettingsPayload = "{}", IsActive = true
        }]);
        var stagedAccessor = scope.ServiceProvider.GetRequiredService<IGetStagedIngressConfigurationAccessor>();
        Assert.Single(await stagedAccessor.GetStagedConfigurationAsync(pending.Id.ToString(), 3));
        Assert.Empty(await stagedAccessor.GetStagedConfigurationAsync(pending.Id.ToString(), 4));

        await competingContext.RuntimeOrchestrationArtifacts.SingleAsync(x => x.Id == pending.Id);
        var winningTransition = Assert.IsAssignableFrom<IRuntimeArtifactRolloutTransitionRepository>(artifacts);
        var losingTransition = Assert.IsAssignableFrom<IRuntimeArtifactRolloutTransitionRepository>(
            competingScope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>());
        Assert.True(await winningTransition.TryMarkRolloutReady(pending.Id, 3));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            losingTransition.TryMarkRolloutStandupFailed(pending.Id, 3, "late failure"));
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Ready,
            (await competingContext.RuntimeOrchestrationArtifacts.AsNoTracking().SingleAsync(x => x.Id == pending.Id)).Status);

        var history = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactDeploymentHistoryRepository>();
        var competingHistory = competingScope.ServiceProvider.GetRequiredService<IRuntimeArtifactDeploymentHistoryRepository>();
        const string rollbackId = "runtime-rollback:mongo-test";
        await history.Append(new RuntimeArtifactDeploymentRecord
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = pending.Id, OrchestrationDefinitionKey = "staged.orders",
            OrchestrationVersion = "1.0.0", ControlPlaneArtifactId = rollbackId, ReleaseId = rollbackId,
            ArtifactType = "orchestration-version.rollback-requested", ReleaseStrategy = "Rollback",
            RuntimeStatus = RuntimeOrchestrationArtifactStatus.Pending, AppliedOnUtc = DateTime.UtcNow
        });
        await competingContext.RuntimeArtifactDeploymentHistory.SingleAsync(x => x.ControlPlaneArtifactId == rollbackId);
        Assert.True(await ((IRuntimeArtifactRollbackStatusRepository)history).TrySetRollbackStatus(rollbackId, RuntimeOrchestrationArtifactStatus.Ready));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            ((IRuntimeArtifactRollbackStatusRepository)competingHistory).TrySetRollbackStatus(rollbackId, RuntimeOrchestrationArtifactStatus.Failed));
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Ready,
            (await competingContext.RuntimeArtifactDeploymentHistory.AsNoTracking().SingleAsync(x => x.ControlPlaneArtifactId == rollbackId)).RuntimeStatus);

        var archivedArtifact = new RuntimeOrchestrationArtifact
        {
            Id = Id.New(), OrchestrationDefinitionKey = "archived.orders", SourceOrchestrationVersionId = Id.New(),
            ArtifactType = "orchestration-version-snapshot", Version = new SemanticVersion(1, 0, 0),
            ArtifactChecksum = new Checksum("archived-checksum"), ArtifactPayload = new JsonObject(),
            Status = RuntimeOrchestrationArtifactStatus.Retired, LifecycleStatus = RuntimeOrchestrationLifecycleStatus.Archived,
            IsActive = false, IngressGeneration = 9, DeployedOnUtc = DateTime.UtcNow
        };
        await artifacts.Upsert(archivedArtifact);
        await ingress.UpsertForArtifactAsync(archivedArtifact.Id,
        [
            new RuntimeIngressConfiguration { Id = Id.New(), RuntimeOrchestrationArtifactId = archivedArtifact.Id,
                ConfigurationKey = "archived-trigger", IngressKind = IngressKind.Trigger, SettingsPayload = "{}", IsActive = false },
            new RuntimeIngressConfiguration { Id = Id.New(), RuntimeOrchestrationArtifactId = archivedArtifact.Id,
                ConfigurationKey = "archived-backchannel", IngressKind = IngressKind.Backchannel, SettingsPayload = "{}", IsActive = false }
        ]);
        var archivedAccessor = scope.ServiceProvider.GetRequiredService<IGetArchivedBackchannelConfigurationAccessor>();
        Assert.Equal(IngressKind.Backchannel, Assert.Single(await archivedAccessor.GetArchivedBackchannelsAsync(archivedArtifact.Id.ToString(), 9)).IngressKind);
        var archivedActivation = Assert.IsAssignableFrom<IArchivedRecoveryActivationRepository>(artifacts);
        Assert.Equal(10, await archivedActivation.TryActivateArchivedBackchannelsAsync(archivedArtifact.Id, 9));
        Assert.False((await context.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == "archived-trigger")).IsActive);
        Assert.True((await context.RuntimeIngressConfigurations.AsNoTracking().SingleAsync(x => x.ConfigurationKey == "archived-backchannel")).IsActive);
    }

    [Fact]
    public async Task DomainsAndTeamsFilterCaseInsensitivelyBeforePaging()
    {
        await using var mongo = new ContainerBuilder()
            .WithImage(MongoImage)
            .WithPortBinding(27017, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(27017))
            .Build();
        await mongo.StartAsync();

        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<Sieve.Models.SieveOptions>(_ => { });
        services.AddOrchestratorControlPlaneStorageEntityFramework(options =>
            options.UseMongoDB($"mongodb://{mongo.Hostname}:{mongo.GetMappedPublicPort(27017)}", "catalog_tests"));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        await context.Database.EnsureCreatedAsync();
        var domains = scope.ServiceProvider.GetRequiredService<IDomainRepository>();
        var teams = scope.ServiceProvider.GetRequiredService<ITeamRepository>();
        foreach (var (key, active) in new[] { ("match_1", false), ("match_2", true), ("match_3", true), ("other", true) })
        {
            await domains.Upsert(new Domain { Id = Id.New(), Key = key, DisplayName = key, Description = "", IsActive = active, CreatedOnUtc = DateTime.UtcNow });
            await teams.Upsert(new Team { Id = Id.New(), Key = key, DisplayName = key, Description = "", IsActive = active, CreatedOnUtc = DateTime.UtcNow });
        }

        var settings = new PagedSettings(1, 1, [], [new QuerySort("displayName", false), new QuerySort("key", false)]);
        var domainPage = await domains.SearchActive(settings, "MATCH");
        var teamPage = await teams.SearchActive(1, 1, "MATCH");
        Assert.Equal(2, domainPage.TotalRows);
        Assert.Equal("match_2", Assert.Single(domainPage.Rows).Key);
        Assert.Equal(2, teamPage.TotalRows);
        Assert.Equal("match_2", Assert.Single(teamPage.Rows).Key);
        Assert.Equal("match_3", Assert.Single((await teams.SearchActive(2, 1, "MATCH")).Rows).Key);
        Assert.Equal(3, (await teams.GetAll(1, 10, "MATCH")).TotalRows);
        var members = scope.ServiceProvider.GetRequiredService<ITeamMemberRepository>();
        var teamId = Assert.Single(teamPage.Rows).Id;
        var emptyTeamId = Assert.Single((await teams.SearchActive(2, 1, "MATCH")).Rows).Id;
        await members.Add(new TeamMember { Id = Id.New(), TeamId = teamId, ExternalUserId = "user", DisplayName = "User" });
        var counts = await members.CountByTeams([teamId, emptyTeamId, teamId]);
        Assert.Equal(1, counts[teamId]);
        Assert.False(counts.ContainsKey(emptyTeamId));
        var releases = scope.ServiceProvider.GetRequiredService<IReleaseRepository>();
        var latest = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var environmentId = Id.New();
        context.DistributionEnvironments.Add(new DistributionEnvironmentEntity { Id = environmentId, Name = "Test", Code = "test", Description = "", IsEnabled = true });
        await context.SaveChangesAsync();
        var nodeId = Id.New();
        await scope.ServiceProvider.GetRequiredService<IRuntimeNodeRepository>().Create(new RuntimeNode
        {
            Id = nodeId, Name = "Node", Code = "test-node", EnvironmentId = environmentId,
            Description = "Test", RegisteredAtUtc = latest, Status = RuntimeNodeStatus.Enabled
        });
        context.OrchestrationAllowedRuntimeNodes.Add(new OrchestrationAllowedRuntimeNodeEntity
        {
            Id = Id.New(), RuntimeNodeId = nodeId, OrchestrationDefinitionId = "sales", CreatedAtUtc = latest, CreatedBy = "test"
        });
        await context.SaveChangesAsync();
        var nodes = scope.ServiceProvider.GetRequiredService<IRuntimeNodeRepository>();
        Assert.Equal(nodeId, Assert.Single((await nodes.SearchEnabled(new PagedSettings(1, 1, [], []), "Node", "", false)).Rows).Id);
        Assert.Equal(nodeId, Assert.Single((await nodes.SearchAllowed(new PagedSettings(1, 1, [], []), "sales", "Node")).Rows).Id);
        Assert.Equal(nodeId, Assert.Single((await nodes.SearchAllowed(new PagedSettings(1, 1, [], []), "sales", "")).Rows).Id);
        Assert.Empty((await nodes.SearchAllowed(new PagedSettings(1, 1, [], []), "other", "Node")).Rows);
        Assert.Empty((await nodes.SearchAllowed(new PagedSettings(1, 1, [], []), "other", "")).Rows);
        var targetRepository = scope.ServiceProvider.GetRequiredService<IReleaseTargetRepository>();
        var releaseId = Id.New();
        var artifactId = Id.New();
        await releases.Create(new Release { Id = releaseId, ArtifactId = artifactId, OrchestrationDefinitionId = "sales",
            RequestedBy = "test", Strategy = "Direct", Status = ReleaseStatus.InProgress, CreatedAtUtc = latest },
            [new ReleasePlanTarget { Id = Id.New(), ReleaseId = releaseId, RuntimeNodeId = nodeId,
                Status = ReleaseStatus.InProgress, CreatedAtUtc = latest, Notes = "" }]);
        await targetRepository.Create(new ReleaseTarget { Id = Id.New(), ReleaseId = releaseId, ArtifactId = artifactId,
            RuntimeNodeId = nodeId, Status = ReleaseTargetStatus.Activated, ActivationStatus = ActivationStatus.Activated,
            RolloutGroup = "Direct", AssignedAtUtc = latest });
        var failedReleaseId = Id.New();
        await releases.Create(new Release { Id = failedReleaseId, ArtifactId = artifactId, OrchestrationDefinitionId = "sales",
            RequestedBy = "test", Strategy = "Direct", Status = ReleaseStatus.InProgress, CreatedAtUtc = latest.AddDays(-1) },
            [new ReleasePlanTarget { Id = Id.New(), ReleaseId = failedReleaseId, RuntimeNodeId = nodeId,
                Status = ReleaseStatus.Failed, CreatedAtUtc = latest, Notes = "" }]);
        var summary = Assert.Single(await releases.GetSummaries(["sales", "missing", "sales"]));
        Assert.Equal(2, summary.Count);
        Assert.Equal(latest, summary.LatestCreatedAtUtc);
        Assert.Empty(await releases.GetSummaries([]));
        var targets = await releases.SearchTargetDetails(releaseId, new PagedSettings(1, 25, [], []));
        Assert.Equal("Node", Assert.Single(targets.Rows).Name);
        Assert.Equal("Test", Assert.Single(targets.Rows).Environment);
        Assert.Empty((await releases.SearchTargetDetails(releaseId, new PagedSettings(2, 25, [], []))).Rows);
        var failedTarget = Assert.Single((await releases.SearchTargetDetails(failedReleaseId, new PagedSettings(1, 25, [], []))).Rows);
        Assert.Equal("Failed", failedTarget.Status);
        Assert.Equal("", failedTarget.TargetId);
        Assert.True(Assert.Single(await releases.GetEffectiveStatuses([releaseId])).AllActivated);
        var failed = Assert.Single(await releases.GetEffectiveStatuses([failedReleaseId]));
        Assert.False(failed.AllActivated);
        Assert.True(failed.AnyFailed);
        Assert.Empty(await releases.GetEffectiveStatuses([Id.New()]));
        await targetRepository.Create(new ReleaseTarget { Id = Id.New(), ReleaseId = releaseId, ArtifactId = artifactId,
            RuntimeNodeId = nodeId, Status = ReleaseTargetStatus.Failed, ActivationStatus = ActivationStatus.ActivationFailed,
            RolloutGroup = "Direct", AssignedAtUtc = latest.AddMinutes(1) });
        var latestStatus = Assert.Single(await releases.GetEffectiveStatuses([releaseId]));
        Assert.False(latestStatus.AllActivated);
        Assert.True(latestStatus.AnyFailed);
        Assert.Equal("Failed", Assert.Single((await releases.SearchTargetDetails(releaseId, new PagedSettings(1, 25, [], []))).Rows).Status);
        Assert.Empty((await domains.GetAll(settings, "%")).Rows);
        Assert.Empty((await teams.GetAll(1, 10, "%")).Rows);
    }
}
