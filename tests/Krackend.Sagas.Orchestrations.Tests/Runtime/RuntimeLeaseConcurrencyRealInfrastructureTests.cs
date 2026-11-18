namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(RuntimeRealInfrastructureCollection.Name)]
public sealed class RuntimeLeaseConcurrencyRealInfrastructureTests(RuntimeRealInfrastructureFixture infrastructure)
{
    [Fact(Timeout = 120_000)]
    public async Task LeaseRenewalDoesNotUseTheContextExecutingTheSaga()
    {
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(infrastructure,
            configureTestServices: services => services.Configure<OrchestrationCoordinationOptions>(options => options.LeaseDurationSeconds = 1));
        await using var scope = runtime.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var coordinator = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceCoordinator>();
        var now = DateTime.UtcNow;
        var instance = new OrchestrationInstance
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = Id.New(), TriggerIntakeId = Id.New(),
            OrchestrationDefinitionKey = "lease-overlap-test", CorrelationId = Guid.NewGuid().ToString("N"),
            SagaId = Id.New().ToString(), ExecutionKey = Guid.NewGuid().ToString("N"),
            Status = OrchestrationInstanceStatus.Running, StartedOnUtc = now, LastUpdatedOnUtc = now
        };
        await repository.Create(instance);

        var acquired = await coordinator.TryExecuteAsync(instance.Id, async token =>
        {
            // Keep the saga's SQL command in flight for several lease renewals.
            await db.Database.ExecuteSqlRawAsync("WAITFOR DELAY '00:00:02'", token);
        });

        Assert.True(acquired);
        var persisted = await repository.GetById(instance.Id);
        Assert.Null(persisted.ActiveLeaseId);
        Assert.Equal(OrchestrationInstanceStatus.Running, persisted.Status);
    }

    [Fact(Timeout = 120_000)]
    public async Task IndependentInstancesRenewLeasesWhileRunningSqlCommandsConcurrently()
    {
        const int instanceCount = 10;
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(infrastructure,
            configureTestServices: services => services.Configure<OrchestrationCoordinationOptions>(options => options.LeaseDurationSeconds = 2));
        await using var setupScope = runtime.Services.CreateAsyncScope();
        var repository = setupScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var instances = Enumerable.Range(0, instanceCount).Select(_ => CreateInstance()).ToArray();
        foreach (var instance in instances) await repository.Create(instance);

        var allEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;
        var tasks = instances.Select(async instance =>
        {
            await using var scope = runtime.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
            var coordinator = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceCoordinator>();
            return await coordinator.TryExecuteAsync(instance.Id, async token =>
            {
                if (Interlocked.Increment(ref entered) == instanceCount) allEntered.TrySetResult();
                await allEntered.Task.WaitAsync(TimeSpan.FromSeconds(20), token);
                await db.Database.ExecuteSqlRawAsync("WAITFOR DELAY '00:00:03'", token);
            });
        }).ToArray();

        var acquired = await Task.WhenAll(tasks);
        Assert.Equal(instanceCount, entered);
        Assert.All(acquired, value => Assert.True(value));
        foreach (var instance in instances)
            Assert.Null((await repository.GetById(instance.Id)).ActiveLeaseId);
    }

    [Fact(Timeout = 120_000)]
    public async Task RenewedLeaseExcludesOtherScopesUntilTheOperationFinishes()
    {
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(infrastructure,
            configureTestServices: services => services.Configure<OrchestrationCoordinationOptions>(options => options.LeaseDurationSeconds = 2));
        await using var firstScope = runtime.Services.CreateAsyncScope();
        await using var secondScope = runtime.Services.CreateAsyncScope();
        var repository = firstScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var first = firstScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceCoordinator>();
        var second = secondScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceCoordinator>();
        var instance = CreateInstance();
        await repository.Create(instance);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var active = first.TryExecuteAsync(instance.Id, async token =>
        {
            entered.SetResult();
            await finish.Task.WaitAsync(token);
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            // More than the original lease duration, so success requires renewal.
            await Task.Delay(TimeSpan.FromSeconds(3));
            var ran = false;
            var acquiredWhileHeld = await second.TryExecuteAsync(instance.Id, _ =>
            {
                ran = true;
                return Task.CompletedTask;
            }, waitForLease: false);
            Assert.False(acquiredWhileHeld);
            Assert.False(ran);
        }
        finally
        {
            finish.TrySetResult();
            Assert.True(await active);
        }

        Assert.True(await second.TryExecuteAsync(instance.Id, _ => Task.CompletedTask, waitForLease: false));
    }

    private static OrchestrationInstance CreateInstance() => new()
    {
        Id = Id.New(), RuntimeOrchestrationArtifactId = Id.New(), TriggerIntakeId = Id.New(),
        OrchestrationDefinitionKey = "lease-overlap-test", CorrelationId = Guid.NewGuid().ToString("N"),
        SagaId = Id.New().ToString(), ExecutionKey = Guid.NewGuid().ToString("N"),
        Status = OrchestrationInstanceStatus.Running, StartedOnUtc = DateTime.UtcNow, LastUpdatedOnUtc = DateTime.UtcNow
    };
}
