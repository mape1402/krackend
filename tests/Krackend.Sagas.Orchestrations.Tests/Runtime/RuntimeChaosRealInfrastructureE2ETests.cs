namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure.RealMessagingArtifactFactory;

[Collection(RuntimeRealInfrastructureCollection.Name)]
public sealed class RuntimeChaosRealInfrastructureE2ETests
{
    private readonly RuntimeRealInfrastructureFixture _infrastructure;

    public RuntimeChaosRealInfrastructureE2ETests(RuntimeRealInfrastructureFixture infrastructure)
    {
        _infrastructure = infrastructure;
    }

    [Fact(Timeout = 300_000)]
    public async Task RuntimeRetriesArtifactStandupAfterRabbitMqRestart()
    {
        var suffix = NewSuffix();
        var databaseName = _infrastructure.CreateDatabaseName();
        var version = new SemanticVersion(3, 0, 0);
        var triggerTopic = Topic(suffix, "events.rabbit.standup.requested");
        var taskTopic = Topic(suffix, "commands.rabbit.standup.process");
        var orchestrationKey = Topic(suffix, "rabbit.standup");
        var rabbitStopped = false;
        RealMessagingRuntimeHarness? runtime = null;
        RealMessagingServiceHost? services = null;

        try
        {
            runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure, databaseName);

            await _infrastructure.StopRabbitMqAsync();
            rabbitStopped = true;

            var artifact = Artifact(
                orchestrationKey,
                version,
                $"chaos-rabbit-standup-{suffix}",
                [EventTrigger(triggerTopic, version)],
                Stage("process", 1, MessagingTask("rabbit.standup.process", 1, version, topic: taskTopic)));

            var deployment = await DeployAsync(runtime, CreatePackage(artifact));
            Assert.True(deployment.Accepted, deployment.Message);
            Assert.Equal(RuntimeOrchestrationArtifactStatus.Pending.ToString(), deployment.Status);

            await Task.Delay(TimeSpan.FromSeconds(2));
            await _infrastructure.StartRabbitMqAsync();
            rabbitStopped = false;

            await runtime.DisposeAsync();
            runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure, databaseName);
            services = await RealMessagingServiceHost.StartAsync(
                _infrastructure,
                Endpoints(version, taskTopic));

            await runtime.WaitForArtifactReadyAsync(deployment.RuntimeArtifactId);

            var correlationId = $"corr-{suffix}-rabbit-standup";
            await runtime.PublishTriggerAsync(
                triggerTopic,
                version.ToString(),
                BusinessPayload(("saleId", $"S-{suffix}")),
                correlationId);

            await runtime.WaitForInstanceStatusAsync(
                correlationId,
                OrchestrationInstanceStatus.Completed,
                TimeSpan.FromSeconds(120));
            await WaitForInvocationCountAsync(services.Scenario, taskTopic, 1);
        }
        finally
        {
            if (rabbitStopped)
            {
                await _infrastructure.StartRabbitMqAsync();
            }

            if (services is not null)
            {
                await services.DisposeAsync();
            }

            if (runtime is not null)
            {
                await runtime.DisposeAsync();
            }
        }
    }

    [Fact(Timeout = 300_000)]
    public async Task RuntimeCompletesInFlightServiceCallbackAfterRabbitMqRestart()
    {
        var suffix = NewSuffix();
        var databaseName = _infrastructure.CreateDatabaseName();
        var version = new SemanticVersion(3, 1, 0);
        var triggerTopic = Topic(suffix, "events.rabbit.inflight.requested");
        var taskTopic = Topic(suffix, "commands.rabbit.inflight.process");
        var orchestrationKey = Topic(suffix, "rabbit.inflight");
        var rabbitStopped = false;
        RealMessagingRuntimeHarness? runtime = null;
        RealMessagingServiceHost? services = null;

        try
        {
            services = await RealMessagingServiceHost.StartAsync(
                _infrastructure,
                Endpoints(version, taskTopic));
            runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure, databaseName);

            services.Scenario.Enqueue(
                taskTopic,
                RealMessagingServiceOutcome.DelayedSuccess(
                    BusinessPayload(("processed", true)),
                    TimeSpan.FromSeconds(8)));

            var artifact = Artifact(
                orchestrationKey,
                version,
                $"chaos-rabbit-inflight-{suffix}",
                [EventTrigger(triggerTopic, version)],
                Stage(
                    "process",
                    1,
                    MessagingTask(
                        "rabbit.inflight.process",
                        1,
                        version,
                        topic: taskTopic,
                        timeoutPolicy: ReconcileTimeoutPolicy(
                            Duration.FromSeconds(10),
                            RetryPolicy(1, "TaskTimedOut")))));

            await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
            var correlationId = $"corr-{suffix}-rabbit-inflight";
            await runtime.PublishTriggerAsync(
                triggerTopic,
                version.ToString(),
                BusinessPayload(("saleId", $"S-{suffix}")),
                correlationId);

            await WaitForInvocationCountAsync(services.Scenario, taskTopic, 1);

            await _infrastructure.StopRabbitMqAsync();
            rabbitStopped = true;

            await services.DisposeAsync();
            services = null;
            await runtime.DisposeAsync();
            runtime = null;

            await Task.Delay(TimeSpan.FromSeconds(3));
            await _infrastructure.StartRabbitMqAsync();
            rabbitStopped = false;

            services = await RealMessagingServiceHost.StartAsync(
                _infrastructure,
                Endpoints(version, taskTopic));
            runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure, databaseName);

            try
            {
                await runtime.WaitForInstanceStatusAsync(
                    correlationId,
                    OrchestrationInstanceStatus.Completed,
                    TimeSpan.FromSeconds(180));
            }
            catch (TimeoutException exception)
            {
                var attempts = await AttemptSummariesAsync(runtime, "rabbit.inflight.process");
                var transitions = await TransitionSummariesAsync(runtime, correlationId);
                throw new TimeoutException(
                    $"{exception.Message}. Attempts: {string.Join("; ", attempts.Select(attempt => attempt.ToString()))}. Transitions: {string.Join("; ", transitions.Select(transition => transition.ToString()))}",
                    exception);
            }
            await WaitForInvocationCountAsync(services.Scenario, taskTopic, 1);
        }
        finally
        {
            if (rabbitStopped)
            {
                await _infrastructure.StartRabbitMqAsync();
            }

            if (services is not null)
            {
                await services.DisposeAsync();
            }

            if (runtime is not null)
            {
                await runtime.DisposeAsync();
            }
        }
    }

    [Fact(Timeout = 300_000)]
    public async Task RuntimeRecoversTriggerAfterRedisOutageAndRuntimeRestart()
    {
        var suffix = NewSuffix();
        var databaseName = _infrastructure.CreateDatabaseName();
        var version = new SemanticVersion(3, 2, 0);
        var triggerTopic = Topic(suffix, "events.redis.intake.requested");
        var taskTopic = Topic(suffix, "commands.redis.intake.process");
        var orchestrationKey = Topic(suffix, "redis.intake");
        var redisStopped = false;
        RealMessagingRuntimeHarness? runtime = null;

        await using var services = await RealMessagingServiceHost.StartAsync(
            _infrastructure,
            Endpoints(version, taskTopic));
        runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure, databaseName);

        try
        {
            var artifact = Artifact(
                orchestrationKey,
                version,
                $"chaos-redis-intake-{suffix}",
                [EventTrigger(triggerTopic, version)],
                Stage("process", 1, MessagingTask("redis.intake.process", 1, version, topic: taskTopic)));

            await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
            var correlationId = $"corr-{suffix}-redis-intake";

            await _infrastructure.StopRedisAsync();
            redisStopped = true;

            await runtime.PublishTriggerAsync(
                triggerTopic,
                version.ToString(),
                BusinessPayload(("saleId", $"S-{suffix}")),
                correlationId);

            await Task.Delay(TimeSpan.FromSeconds(3));
            await _infrastructure.StartRedisAsync();
            redisStopped = false;

            await runtime.DisposeAsync();
            runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure, databaseName);

            await runtime.WaitForInstanceStatusAsync(
                correlationId,
                OrchestrationInstanceStatus.Completed,
                TimeSpan.FromSeconds(180));
            await WaitForInvocationCountAsync(services.Scenario, taskTopic, 1);
        }
        finally
        {
            if (redisStopped)
            {
                await _infrastructure.StartRedisAsync();
            }

            if (runtime is not null)
            {
                await runtime.DisposeAsync();
            }
        }
    }

    [Fact(Timeout = 300_000)]
    public async Task RuntimeReprocessesCallbackAfterSqlServerRestart()
    {
        var suffix = NewSuffix();
        var databaseName = _infrastructure.CreateDatabaseName();
        var version = new SemanticVersion(3, 3, 0);
        var triggerTopic = Topic(suffix, "events.sql.callback.requested");
        var taskTopic = Topic(suffix, "commands.sql.callback.process");
        var orchestrationKey = Topic(suffix, "sql.callback");
        var sqlStopped = false;
        RealMessagingRuntimeHarness? runtime = null;

        await using var services = await RealMessagingServiceHost.StartAsync(
            _infrastructure,
            Endpoints(version, taskTopic));
        runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure, databaseName);

        try
        {
            services.Scenario.Enqueue(
                taskTopic,
                RealMessagingServiceOutcome.DelayedSuccess(
                    BusinessPayload(("stored", true)),
                    TimeSpan.FromSeconds(2)));

            var artifact = Artifact(
                orchestrationKey,
                version,
                $"chaos-sql-callback-{suffix}",
                [EventTrigger(triggerTopic, version)],
                Stage("process", 1, MessagingTask("sql.callback.process", 1, version, topic: taskTopic)));

            await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
            var correlationId = $"corr-{suffix}-sql-callback";
            await runtime.PublishTriggerAsync(
                triggerTopic,
                version.ToString(),
                BusinessPayload(("saleId", $"S-{suffix}")),
                correlationId);

            await WaitForInvocationCountAsync(services.Scenario, taskTopic, 1);

            await _infrastructure.StopSqlServerAsync();
            sqlStopped = true;
            await Task.Delay(TimeSpan.FromSeconds(5));
            await _infrastructure.StartSqlServerAsync();
            sqlStopped = false;

            await runtime.DisposeAsync();
            runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure, databaseName);

            await runtime.WaitForInstanceStatusAsync(
                correlationId,
                OrchestrationInstanceStatus.Completed,
                TimeSpan.FromSeconds(180));

            var attempts = await AttemptsForTaskAsync(runtime, "sql.callback.process");
            Assert.Contains(attempts, attempt =>
                attempt.Status == TaskExecutionStatus.Completed &&
                attempt.ResponsePayload?["stored"]?.GetValue<bool>() == true);
        }
        finally
        {
            if (sqlStopped)
            {
                await _infrastructure.StartSqlServerAsync();
            }

            if (runtime is not null)
            {
                await runtime.DisposeAsync();
            }
        }
    }

    private static Task<RuntimeArtifactDeploymentResult> DeployAsync(
        RealMessagingRuntimeHarness runtime,
        RuntimeArtifactDeliveryPackage package)
    {
        var scope = runtime.Services.CreateAsyncScope();
        return DeployWithScopeAsync(scope, package);
    }

    private static async Task<RuntimeArtifactDeploymentResult> DeployWithScopeAsync(
        AsyncServiceScope scope,
        RuntimeArtifactDeliveryPackage package)
    {
        await using (scope)
        {
            return await scope.ServiceProvider
                .GetRequiredService<IRuntimeArtifactDeploymentService>()
                .DeployAsync(package, "chaos-e2e");
        }
    }

    private static RealMessagingConsumerEndpoint[] Endpoints(
        SemanticVersion version,
        params string[] topics)
        => topics.Select(topic => new RealMessagingConsumerEndpoint(topic, version.ToString())).ToArray();

    private static string NewSuffix()
        => Guid.NewGuid().ToString("N")[..10];

    private static string Topic(string suffix, string name)
        => $"chaos.{suffix}.{name}";

    private static Task<IReadOnlyCollection<RealMessagingServiceInvocation>> WaitForInvocationCountAsync(
        RealMessagingScenario scenario,
        string topic,
        int expectedCount)
        => PollingAssert.EventuallyAsync(
            () => Task.FromResult(scenario.Invocations),
            invocations => invocations.Count(invocation => string.Equals(invocation.Topic, topic, StringComparison.OrdinalIgnoreCase)) >= expectedCount,
            $"Expected at least {expectedCount} service invocations for topic '{topic}'",
            TimeSpan.FromSeconds(90));

    private static Task<TaskExecutionAttempt[]> AttemptsForTaskAsync(
        RealMessagingRuntimeHarness runtime,
        string taskKey)
        => runtime.QueryAsync(async dbContext =>
        {
            var query =
                from attempt in dbContext.TaskExecutionAttempts.AsNoTracking()
                join task in dbContext.TaskExecutions.AsNoTracking()
                    on attempt.TaskExecutionId equals task.Id
                where task.TaskKey == taskKey
                orderby attempt.AttemptNumber
                select attempt;

            return await query.ToArrayAsync();
        });

    private static Task<AttemptSummary[]> AttemptSummariesAsync(
        RealMessagingRuntimeHarness runtime,
        string taskKey)
        => runtime.QueryAsync(async dbContext =>
        {
            var query =
                from attempt in dbContext.TaskExecutionAttempts.AsNoTracking()
                join task in dbContext.TaskExecutions.AsNoTracking()
                    on attempt.TaskExecutionId equals task.Id
                where task.TaskKey == taskKey
                orderby attempt.AttemptNumber
                select new AttemptSummary(
                    attempt.AttemptNumber,
                    attempt.Status.ToString(),
                    attempt.ErrorCode,
                    attempt.ErrorMessage);

            return await query.ToArrayAsync();
        });

    private static Task<TransitionSummary[]> TransitionSummariesAsync(
        RealMessagingRuntimeHarness runtime,
        string correlationId)
        => runtime.QueryAsync(async dbContext =>
        {
            var query =
                from transition in dbContext.ExecutionTransitions.AsNoTracking()
                join instance in dbContext.OrchestrationInstances.AsNoTracking()
                    on transition.OrchestrationInstanceId equals instance.Id
                where instance.CorrelationId == correlationId
                orderby transition.OccurredOnUtc
                select new TransitionSummary(
                    transition.TransitionType,
                    transition.FromStatus,
                    transition.ToStatus,
                    transition.Message);

            return await query.ToArrayAsync();
        });

    private sealed record AttemptSummary(
        int AttemptNumber,
        string Status,
        string? ErrorCode,
        string? ErrorMessage);

    private sealed record TransitionSummary(
        string Type,
        string From,
        string To,
        string? Message);
}
