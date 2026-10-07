namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon;
using Krackend.Sagas.Orchestrations.Runtime.Operations;
using Krackend.Sagas.Orchestrations.Runtime.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

public sealed class RuntimeDisasterRecoveryTests
{
    [Theory]
    [InlineData(RuntimeOperationalState.Healthy, RuntimeAdmissionOperation.TriggerIntake, true)]
    [InlineData(RuntimeOperationalState.Degraded, RuntimeAdmissionOperation.Dispatch, true)]
    [InlineData(RuntimeOperationalState.Recovering, RuntimeAdmissionOperation.Reconciliation, true)]
    [InlineData(RuntimeOperationalState.Recovering, RuntimeAdmissionOperation.OrchestrationExecution, true)]
    [InlineData(RuntimeOperationalState.Recovering, RuntimeAdmissionOperation.Dispatch, true)]
    [InlineData(RuntimeOperationalState.Recovering, RuntimeAdmissionOperation.TriggerIntake, false)]
    [InlineData(RuntimeOperationalState.Closed, RuntimeAdmissionOperation.BackchannelIntake, false)]
    [InlineData((RuntimeOperationalState)999, RuntimeAdmissionOperation.TriggerIntake, false)]
    public async Task AdmissionControllerAppliesDisasterRecoveryAdmissionRules(
        RuntimeOperationalState state,
        RuntimeAdmissionOperation operation,
        bool expected)
    {
        var provider = new FixedRuntimeOperationalStateProvider(state);
        var controller = new DefaultRuntimeAdmissionController(provider);

        Assert.Equal(expected, controller.CanAccept(operation));
        if (expected)
        {
            await controller.EnsureAcceptedAsync(operation);
        }
        else
        {
            var exception = await Assert.ThrowsAsync<RuntimeAdmissionRejectedException>(() =>
                controller.EnsureAcceptedAsync(operation).AsTask());
            Assert.Equal(operation, exception.Operation);
            Assert.Equal(state, exception.Snapshot.State);
        }
    }

    [Fact]
    public async Task OperationalStateControllerClosesForCriticalDependencyAndDegradesForOptionalDependency()
    {
        var handler = new RecordingDegradationHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRuntimeDependencyProbe>(new TestDependencyProbe("primary", RuntimeDependencyKind.Critical, false, "db down"));
        services.AddSingleton<IRuntimeDependencyProbe>(new TestDependencyProbe("redis", RuntimeDependencyKind.Optional, false, "redis down"));
        services.AddSingleton<IRuntimeDegradationHandler>(handler);
        await using var provider = services.BuildServiceProvider();
        var controller = new DefaultRuntimeOperationalStateController(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DefaultRuntimeOperationalStateController>.Instance);

        var closed = await controller.EvaluateAsync();

        Assert.Equal(RuntimeOperationalState.Closed, closed.State);
        Assert.Contains(closed.Dependencies, dependency => dependency.Name == "primary" && !dependency.IsAvailable);
        Assert.Single(handler.Changes);

        services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRuntimeDependencyProbe>(new TestDependencyProbe("primary", RuntimeDependencyKind.Critical, true));
        services.AddSingleton<IRuntimeDependencyProbe>(new TestDependencyProbe("redis", RuntimeDependencyKind.Optional, false, "redis down"));
        await using var degradedProvider = services.BuildServiceProvider();
        controller = new DefaultRuntimeOperationalStateController(
            degradedProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DefaultRuntimeOperationalStateController>.Instance);

        var degraded = await controller.EvaluateAsync();

        Assert.Equal(RuntimeOperationalState.Degraded, degraded.State);
        Assert.Contains(degraded.Dependencies, dependency => dependency.Name == "redis" && !dependency.IsAvailable);
    }

    [Fact]
    public async Task OperationalStateControllerMarksRecoveringAndSwallowsHandlerFailures()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRuntimeDegradationHandler, ThrowingDegradationHandler>();
        await using var provider = services.BuildServiceProvider();
        var controller = new DefaultRuntimeOperationalStateController(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DefaultRuntimeOperationalStateController>.Instance);

        var recovering = await controller.MarkRecoveringAsync("db restored");

        Assert.Equal(RuntimeOperationalState.Recovering, recovering.State);
        Assert.Equal("db restored", recovering.Reason);
        Assert.Equal(RuntimeOperationalState.Recovering, controller.Current.State);

        var healthy = await controller.EvaluateAsync();

        Assert.Equal(RuntimeOperationalState.Healthy, healthy.State);
    }

    [Fact]
    public void OperationalStateControllerValidatesConstructorArguments()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        Assert.Throws<ArgumentNullException>(() => new DefaultRuntimeOperationalStateController(
            null!,
            NullLogger<DefaultRuntimeOperationalStateController>.Instance));
        Assert.Throws<ArgumentNullException>(() => new DefaultRuntimeOperationalStateController(
            scopeFactory,
            null!));
    }

    [Fact]
    public async Task OperationalStateControllerDetectsDependencyCountAndContentChanges()
    {
        var handler = new RecordingDegradationHandler();
        var probe = new MutableDependencyProbe("redis", RuntimeDependencyKind.Optional, true);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRuntimeDependencyProbe>(probe);
        services.AddSingleton<IRuntimeDegradationHandler>(handler);
        await using var provider = services.BuildServiceProvider();
        var controller = new DefaultRuntimeOperationalStateController(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DefaultRuntimeOperationalStateController>.Instance);

        await controller.MarkRecoveringAsync(string.Empty);
        var healthy = await controller.EvaluateAsync();
        probe.Available = false;
        probe.Reason = "redis down";
        var degraded = await controller.EvaluateAsync();

        Assert.Equal(RuntimeOperationalState.Healthy, healthy.State);
        Assert.Equal(RuntimeOperationalState.Degraded, degraded.State);
        Assert.Equal("Runtime recovery is running.", handler.Changes[0].Current.Reason);
        Assert.True(handler.Changes.Count >= 3);
    }

    [Fact]
    public void OperationalStateControllerDependencyComparisonHandlesCountAndContentMismatches()
    {
        var comparison = typeof(DefaultRuntimeOperationalStateController).GetMethod(
            "SameDependencies",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        RuntimeDependencyProbeResult[] healthy =
        [
            new("primary", RuntimeDependencyKind.Critical, true)
        ];
        RuntimeDependencyProbeResult[] degraded =
        [
            new("primary", RuntimeDependencyKind.Critical, false, "db down")
        ];

        var countMismatch = (bool)comparison.Invoke(null, [Array.Empty<RuntimeDependencyProbeResult>(), healthy])!;
        var contentMismatch = (bool)comparison.Invoke(null, [healthy, degraded])!;
        var same = (bool)comparison.Invoke(null, [healthy, healthy])!;

        Assert.False(countMismatch);
        Assert.False(contentMismatch);
        Assert.True(same);
    }

    [Fact]
    public async Task OperationalStateControllerTreatsProbeExceptionAsUnavailableAndAvoidsDuplicateTransitions()
    {
        var handler = new RecordingDegradationHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRuntimeDependencyProbe>(new ThrowingDependencyProbe());
        services.AddSingleton<IRuntimeDegradationHandler>(handler);
        await using var provider = services.BuildServiceProvider();
        var controller = new DefaultRuntimeOperationalStateController(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DefaultRuntimeOperationalStateController>.Instance);

        var first = await controller.EvaluateAsync();
        var second = await controller.EvaluateAsync();

        Assert.Equal(RuntimeOperationalState.Closed, first.State);
        Assert.Same(first, second);
        Assert.Single(handler.Changes);
        Assert.Contains(first.Dependencies, dependency => dependency.Name == "throwing" && !dependency.IsAvailable);
    }

    [Fact]
    public async Task OperationalStateControllerDoesNotFailWhenHandlersCannotBeResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRuntimeDependencyProbe>(new TestDependencyProbe(
            "primary",
            RuntimeDependencyKind.Critical,
            false,
            "db down"));
        services.AddSingleton<IRuntimeDegradationHandler, ConstructorThrowingDegradationHandler>();
        await using var provider = services.BuildServiceProvider();
        var controller = new DefaultRuntimeOperationalStateController(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DefaultRuntimeOperationalStateController>.Instance);

        var snapshot = await controller.EvaluateAsync();

        Assert.Equal(RuntimeOperationalState.Closed, snapshot.State);
    }

    [Fact]
    public async Task OperationalMonitorBackgroundServiceEvaluatesState()
    {
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = Substitute.For<IRuntimeOperationalStateController>();
        controller.EvaluateAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            observed.TrySetResult();
            return Task.FromResult(new RuntimeOperationalSnapshot(
                RuntimeOperationalState.Healthy,
                DateTime.UtcNow,
                Array.Empty<RuntimeDependencyProbeResult>()));
        });
        var service = new RuntimeOperationalMonitorBackgroundService(
            controller,
            new StaticOptionsMonitor<RuntimeOperationalOptions>(new RuntimeOperationalOptions
            {
                Enabled = true,
                ScanIntervalSeconds = 1
            }),
            NullLogger<RuntimeOperationalMonitorBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        await controller.Received().EvaluateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RuntimeReconciliationBackgroundServiceRunsRecoveryBeforeReopening()
    {
        var reconciled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stateController = Substitute.For<IRuntimeOperationalStateController>();
        var reconciler = Substitute.For<IOrchestrationRuntimeReconciler>();
        var artifactRepository = Substitute.For<IRuntimeArtifactRepository>();
        var standupScheduler = Substitute.For<IRuntimeIngressStandupScheduler>();
        var healthy = new RuntimeOperationalSnapshot(
            RuntimeOperationalState.Healthy,
            DateTime.UtcNow,
            Array.Empty<RuntimeDependencyProbeResult>());
        stateController.EvaluateAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(healthy));
        stateController.MarkRecoveringAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RuntimeOperationalSnapshot(
                RuntimeOperationalState.Recovering,
                DateTime.UtcNow,
                Array.Empty<RuntimeDependencyProbeResult>())));
        reconciler.ReconcileAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                reconciled.TrySetResult();
                return Task.FromResult(new RuntimeReconciliationResult(0, 0, 0, false));
            });
        artifactRepository.GetReady(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<RuntimeOrchestrationArtifact>>(Array.Empty<RuntimeOrchestrationArtifact>()));
        var services = new ServiceCollection();
        services.AddScoped(_ => reconciler);
        services.AddScoped(_ => artifactRepository);
        services.AddScoped(_ => standupScheduler);
        await using var provider = services.BuildServiceProvider();
        var service = new RuntimeReconciliationBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            stateController,
            new StaticOptionsMonitor<RuntimeReconciliationOptions>(new RuntimeReconciliationOptions
            {
                Enabled = true,
                ScanIntervalSeconds = 1
            }),
            NullLogger<RuntimeReconciliationBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await reconciled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        await stateController.Received().MarkRecoveringAsync(
            "Automated runtime reconciliation is running.",
            Arg.Any<CancellationToken>());
        await reconciler.Received().ReconcileAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DisasterRecoveryBackgroundServicesValidateConstructorArguments()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var stateController = Substitute.For<IRuntimeOperationalStateController>();
        var operationalOptions = new StaticOptionsMonitor<RuntimeOperationalOptions>(new RuntimeOperationalOptions());
        var reconciliationOptions = new StaticOptionsMonitor<RuntimeReconciliationOptions>(new RuntimeReconciliationOptions());

        Assert.Throws<ArgumentNullException>(() => new RuntimeOperationalMonitorBackgroundService(
            null!,
            operationalOptions,
            NullLogger<RuntimeOperationalMonitorBackgroundService>.Instance));
        Assert.Throws<ArgumentNullException>(() => new RuntimeOperationalMonitorBackgroundService(
            stateController,
            null!,
            NullLogger<RuntimeOperationalMonitorBackgroundService>.Instance));
        Assert.Throws<ArgumentNullException>(() => new RuntimeOperationalMonitorBackgroundService(
            stateController,
            operationalOptions,
            null!));

        Assert.Throws<ArgumentNullException>(() => new RuntimeReconciliationBackgroundService(
            null!,
            stateController,
            reconciliationOptions,
            NullLogger<RuntimeReconciliationBackgroundService>.Instance));
        Assert.Throws<ArgumentNullException>(() => new RuntimeReconciliationBackgroundService(
            scopeFactory,
            null!,
            reconciliationOptions,
            NullLogger<RuntimeReconciliationBackgroundService>.Instance));
        Assert.Throws<ArgumentNullException>(() => new RuntimeReconciliationBackgroundService(
            scopeFactory,
            stateController,
            null!,
            NullLogger<RuntimeReconciliationBackgroundService>.Instance));
        Assert.Throws<ArgumentNullException>(() => new RuntimeReconciliationBackgroundService(
            scopeFactory,
            stateController,
            reconciliationOptions,
            null!));
    }

    [Fact]
    public async Task ReconcilerSkipsWhenRuntimeAdmissionIsClosed()
    {
        var admission = Substitute.For<IRuntimeAdmissionController>();
        admission.CanAccept(RuntimeAdmissionOperation.Reconciliation).Returns(false);
        var reconciler = new DefaultOrchestrationRuntimeReconciler(
            admission,
            Substitute.For<IOrchestrationInstanceRepository>(),
            Substitute.For<IOrchestrationTimeoutProcessor>(),
            Substitute.For<ISagaEngine>(),
            Options.Create(new RuntimeReconciliationOptions()));

        var result = await reconciler.ReconcileAsync(DateTime.UtcNow);

        Assert.True(result.Skipped);
        Assert.Equal("Runtime admission is closed.", result.Reason);
    }

    [Fact]
    public async Task ReconcilerProcessesTimeoutsAndAdvancesRecoverableInstances()
    {
        var now = DateTime.UtcNow;
        var instance = Instance(OrchestrationInstanceStatus.Waiting, now.AddMinutes(-5));
        var admission = Substitute.For<IRuntimeAdmissionController>();
        var repository = Substitute.For<IOrchestrationInstanceRepository>();
        var timeoutProcessor = Substitute.For<IOrchestrationTimeoutProcessor>();
        var sagaEngine = Substitute.For<ISagaEngine>();
        admission.CanAccept(RuntimeAdmissionOperation.Reconciliation).Returns(true);
        admission.EnsureAcceptedAsync(RuntimeAdmissionOperation.Reconciliation, Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);
        timeoutProcessor.ProcessDueTimeoutsAsync(now, Arg.Any<CancellationToken>()).Returns(2);
        repository.GetRecoverable(3, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<OrchestrationInstance>>([instance]));
        var reconciler = new DefaultOrchestrationRuntimeReconciler(
            admission,
            repository,
            timeoutProcessor,
            sagaEngine,
            Options.Create(new RuntimeReconciliationOptions { BatchSize = 3 }));

        var result = await reconciler.ReconcileAsync(now);

        Assert.False(result.Skipped);
        Assert.Equal(1, result.InstancesObserved);
        Assert.Equal(1, result.InstancesAdvanced);
        Assert.Equal(2, result.TimeoutsProcessed);
        await sagaEngine.Received(1).OrchestrateAsync(
            Arg.Is<ForwardIntent>(intent =>
                intent.ArtifactId == instance.RuntimeOrchestrationArtifactId.ToString() &&
                intent.MessageMetadata.OrchestrationInstanceId == instance.Id.ToString() &&
                intent.MessageMetadata.CorrelationId == instance.CorrelationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcilerSkipsTerminalInstancesAndBuildsFallbackMetadata()
    {
        var now = DateTime.UtcNow;
        var terminal = Instance(OrchestrationInstanceStatus.Completed, now.AddMinutes(-10));
        var recoverable = Instance(OrchestrationInstanceStatus.Running, now.AddMinutes(-5));
        recoverable.SagaId = string.Empty;
        recoverable.CurrentTaskKey = string.Empty;
        var admission = Substitute.For<IRuntimeAdmissionController>();
        var repository = Substitute.For<IOrchestrationInstanceRepository>();
        var timeoutProcessor = Substitute.For<IOrchestrationTimeoutProcessor>();
        var sagaEngine = Substitute.For<ISagaEngine>();
        admission.CanAccept(RuntimeAdmissionOperation.Reconciliation).Returns(true);
        admission.EnsureAcceptedAsync(RuntimeAdmissionOperation.Reconciliation, Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);
        timeoutProcessor.ProcessDueTimeoutsAsync(now, Arg.Any<CancellationToken>()).Returns(0);
        repository.GetRecoverable(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<OrchestrationInstance>>([terminal, recoverable]));
        var reconciler = new DefaultOrchestrationRuntimeReconciler(
            admission,
            repository,
            timeoutProcessor,
            sagaEngine,
            Options.Create(new RuntimeReconciliationOptions { BatchSize = 0 }));

        var result = await reconciler.ReconcileAsync(now);

        Assert.Equal(2, result.InstancesObserved);
        Assert.Equal(1, result.InstancesAdvanced);
        await sagaEngine.Received(1).OrchestrateAsync(
            Arg.Is<ForwardIntent>(intent =>
                intent.MessageMetadata.SagaId == recoverable.Id.ToString() &&
                intent.MessageMetadata.CurrentTasks.Length == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InMemoryPrimaryPersistenceProbeIsAlwaysAvailable()
    {
        var probe = new InMemoryRuntimePrimaryPersistenceProbe();

        var result = await probe.CheckAsync();

        Assert.Equal("primary-persistence", result.Name);
        Assert.Equal(RuntimeDependencyKind.Critical, result.Kind);
        Assert.True(result.IsAvailable);
    }

    [Fact]
    public async Task EntityFrameworkPrimaryPersistenceProbeReportsAvailableDatabase()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOrchestratorRuntimeStorageEntityFramework(options => options.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var probe = scope.ServiceProvider
            .GetServices<IRuntimeDependencyProbe>()
            .Single(dependencyProbe => dependencyProbe.Name == "primary-persistence");

        var result = await probe.CheckAsync();

        Assert.Equal("primary-persistence", result.Name);
        Assert.Equal(RuntimeDependencyKind.Critical, result.Kind);
        Assert.True(result.IsAvailable);
    }

    [Fact]
    public async Task RedisGossipProbeReportsOptionalAvailabilityAndFailures()
    {
        var connection = Substitute.For<IConnectionMultiplexer>();
        connection.IsConnected.Returns(true);
        var factory = Substitute.For<IRedisRuntimeGossipConnectionFactory>();
        factory.GetConnectionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(connection));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(factory);
        services.AddKrackendOrchestrationsRuntime()
            .AddRedisGossip(new ConfigurationBuilder().Build());
        await using var provider = services.BuildServiceProvider();
        var probe = provider
            .GetServices<IRuntimeDependencyProbe>()
            .Single(dependencyProbe => dependencyProbe.Name == "redis-gossip");

        var available = await probe.CheckAsync();

        Assert.Equal("redis-gossip", available.Name);
        Assert.Equal(RuntimeDependencyKind.Optional, available.Kind);
        Assert.True(available.IsAvailable);

        factory.GetConnectionAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<IConnectionMultiplexer>(new InvalidOperationException("redis unavailable")));

        var unavailable = await probe.CheckAsync();

        Assert.False(unavailable.IsAvailable);
        Assert.Equal("redis unavailable", unavailable.Reason);
    }

    [Fact]
    public async Task PigeonDegradationHandlerIsRegisteredAndHandlesClosedAndResumeStates()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pigeon:Domain"] = "Krackend.Tests",
                ["Pigeon:MessageBrokers:RabbitMq:Url"] = "amqp://guest:guest@localhost:5672"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddKrackendOrchestrationsRuntime()
            .AddPigeon(configuration);
        await using var provider = services.BuildServiceProvider();
        var handler = provider
            .GetServices<IRuntimeDegradationHandler>()
            .Single(degradationHandler => degradationHandler.GetType().Name == "PigeonRuntimeDegradationHandler");
        var closed = new RuntimeOperationalSnapshot(RuntimeOperationalState.Closed, DateTime.UtcNow, []);
        var healthy = new RuntimeOperationalSnapshot(RuntimeOperationalState.Healthy, DateTime.UtcNow, []);

        await handler.OnRuntimeStateChangedAsync(new RuntimeOperationalStateChangedContext(null, closed));
        await handler.OnRuntimeStateChangedAsync(new RuntimeOperationalStateChangedContext(closed, healthy));
        await handler.OnRuntimeStateChangedAsync(new RuntimeOperationalStateChangedContext(null, healthy));
    }

    [Fact]
    public async Task InMemoryRepositoryReturnsOnlyNonTerminalRecoverableInstancesOldestFirst()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var now = DateTime.UtcNow;
        var running = Instance(OrchestrationInstanceStatus.Running, now.AddMinutes(-10));
        var waiting = Instance(OrchestrationInstanceStatus.Waiting, now.AddMinutes(-5));
        var completed = Instance(OrchestrationInstanceStatus.Completed, now.AddMinutes(-20));
        await repository.Create(waiting);
        await repository.Create(completed);
        await repository.Create(running);

        var recoverable = await repository.GetRecoverable(10);

        Assert.Equal([running.Id, waiting.Id], recoverable.Select(instance => instance.Id).ToArray());
    }

    [Fact]
    public async Task IngressRegistryRejectsStandupWhenRuntimeAdmissionIsClosed()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRuntimeIngressLocalState, RuntimeIngressLocalState>();
        services.AddSingleton<IGetAllIngressConfigurationsAccessor>(Substitute.For<IGetAllIngressConfigurationsAccessor>());
        services.AddSingleton(Substitute.For<IRuntimeAdmissionController>());
        await using var provider = services.BuildServiceProvider();
        var admission = provider.GetRequiredService<IRuntimeAdmissionController>();
        admission
            .EnsureAcceptedAsync(RuntimeAdmissionOperation.IngressStandup, Arg.Any<CancellationToken>())
            .Returns(_ => throw new RuntimeAdmissionRejectedException(
                RuntimeAdmissionOperation.IngressStandup,
                new RuntimeOperationalSnapshot(RuntimeOperationalState.Closed, DateTime.UtcNow, [])));
        var registry = new IngressRegistry(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IRuntimeIngressLocalState>(),
            admission);

        await Assert.ThrowsAsync<RuntimeAdmissionRejectedException>(() =>
            registry.StandUpAllAsync(CancellationToken.None));
    }

    private static OrchestrationInstance Instance(OrchestrationInstanceStatus status, DateTime lastUpdatedOnUtc)
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            RuntimeOrchestrationArtifactId = Id.New(),
            TriggerIntakeId = Id.New(),
            CorrelationId = $"corr-{Guid.NewGuid():N}",
            SagaId = $"saga-{Guid.NewGuid():N}",
            ExecutionKey = "sales.sale.created:1.0.0",
            Status = status,
            CurrentStageKey = "inventories",
            CurrentTaskKey = "reserve",
            StartedOnUtc = lastUpdatedOnUtc.AddMinutes(-1),
            LastUpdatedOnUtc = lastUpdatedOnUtc,
            SnapshotPayload = JsonNode.Parse("""{"saleId":"sale-1"}""")
        };

    private sealed class FixedRuntimeOperationalStateProvider(RuntimeOperationalState state) : IRuntimeOperationalStateProvider
    {
        public RuntimeOperationalSnapshot Current { get; } = new(state, DateTime.UtcNow, Array.Empty<RuntimeDependencyProbeResult>());
    }

    private sealed class TestDependencyProbe(
        string name,
        RuntimeDependencyKind kind,
        bool available,
        string reason = "") : IRuntimeDependencyProbe
    {
        public string Name { get; } = name;

        public RuntimeDependencyKind Kind { get; } = kind;

        public ValueTask<RuntimeDependencyProbeResult> CheckAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new RuntimeDependencyProbeResult(Name, Kind, available, reason));
    }

    private sealed class ThrowingDependencyProbe : IRuntimeDependencyProbe
    {
        public string Name => "throwing";

        public RuntimeDependencyKind Kind => RuntimeDependencyKind.Critical;

        public ValueTask<RuntimeDependencyProbeResult> CheckAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("probe failed");
    }

    private sealed class MutableDependencyProbe(
        string name,
        RuntimeDependencyKind kind,
        bool available) : IRuntimeDependencyProbe
    {
        public string Name { get; } = name;

        public RuntimeDependencyKind Kind { get; } = kind;

        public bool Available { get; set; } = available;

        public string Reason { get; set; } = string.Empty;

        public ValueTask<RuntimeDependencyProbeResult> CheckAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new RuntimeDependencyProbeResult(Name, Kind, Available, Reason));
    }

    private sealed class RecordingDegradationHandler : IRuntimeDegradationHandler
    {
        public List<RuntimeOperationalStateChangedContext> Changes { get; } = [];

        public ValueTask OnRuntimeStateChangedAsync(
            RuntimeOperationalStateChangedContext context,
            CancellationToken cancellationToken = default)
        {
            Changes.Add(context);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingDegradationHandler : IRuntimeDegradationHandler
    {
        public ValueTask OnRuntimeStateChangedAsync(
            RuntimeOperationalStateChangedContext context,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("handler failed");
    }

    private sealed class ConstructorThrowingDegradationHandler : IRuntimeDegradationHandler
    {
        public ConstructorThrowingDegradationHandler()
            => throw new InvalidOperationException("handler unavailable");

        public ValueTask OnRuntimeStateChangedAsync(
            RuntimeOperationalStateChangedContext context,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class StaticOptionsMonitor<TOptions>(TOptions currentValue) : IOptionsMonitor<TOptions>
    {
        public TOptions CurrentValue { get; } = currentValue;

        public TOptions Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<TOptions, string> listener) => new NoopDisposable();
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
