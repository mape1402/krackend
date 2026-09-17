namespace Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.ButterMorph.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mule;
using Mule.EntityFrameworkCore;
using Pigeon.Messaging.Consuming.Management;
using Pigeon.Messaging.Producing;
using Pigeon.Messaging.Rabbit;
using Pigeon.Messaging.Topology;
using PigeonVersion = Pigeon.Messaging.Contracts.SemanticVersion;

internal sealed class RealMessagingRuntimeHarness : IAsyncDisposable
{
    private readonly IHost _host;

    private RealMessagingRuntimeHarness(IHost host, string databaseName, string connectionString)
    {
        _host = host;
        DatabaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string DatabaseName { get; }

    public string ConnectionString { get; }

    public IServiceProvider Services => _host.Services;

    public static async Task<RealMessagingRuntimeHarness> StartAsync(
        RuntimeRealInfrastructureFixture infrastructure,
        string? databaseName = null,
        string? replicaId = null,
        bool gossipEnabled = false,
        bool butterMorphEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(infrastructure);

        var resolvedDatabaseName = string.IsNullOrWhiteSpace(databaseName)
            ? infrastructure.CreateDatabaseName()
            : databaseName;
        var connectionString = infrastructure.BuildSqlConnectionString(resolvedDatabaseName);
        var resolvedReplicaId = string.IsNullOrWhiteSpace(replicaId)
            ? $"runtime-{Guid.NewGuid():N}"
            : replicaId;

        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:RabbitMq"] = infrastructure.RabbitMqConnectionString,
            ["ConnectionStrings:Redis"] = infrastructure.RedisConnectionString,
            ["ConnectionStrings:Mule"] = connectionString,
            ["Pigeon:Domain"] = "Krackend.RealMessagingRuntimeHarness",
            ["Pigeon:MessageBrokers:RabbitMq:Url"] = infrastructure.RabbitMqConnectionString,
            ["Pigeon:MessageBrokers:RabbitMq:ConnectionString"] = infrastructure.RabbitMqConnectionString,
            ["Runtime:Replica:ReplicaId"] = resolvedReplicaId,
            ["Runtime:Gossip:Enabled"] = gossipEnabled.ToString(),
            ["Runtime:Gossip:RedisConnectionString"] = infrastructure.RedisConnectionString,
            ["Runtime:Timeouts:Enabled"] = "true",
            ["Runtime:Timeouts:ScanIntervalSeconds"] = "1"
        };

        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(configuration =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(settings);
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddOrchestratorRuntimeStorageEntityFramework(options =>
                {
                    options.UseSqlServer(connectionString);
                });

                var runtime = services
                    .AddKrackendOrchestrationsRuntime()
                    .AddPigeon(context.Configuration, pigeon =>
                    {
                        pigeon.SetTopologyProvisioningMode(
                            TopologyProvisioningMode.OnStartup |
                            TopologyProvisioningMode.OnPublish |
                            TopologyProvisioningMode.OnConsume);
                        pigeon.ConfigureConsumerExecution(execution =>
                        {
                            execution.AcknowledgementMode = MessageAcknowledgementMode.OnHandlerSuccess;
                            execution.MaxConcurrency = null;
                            execution.QueueCapacity = null;
                            execution.PrefetchCount = null;
                        });
                        pigeon.UseRabbitMq(rabbit =>
                        {
                            rabbit.Url = infrastructure.RabbitMqConnectionString;
                        });
                    });

                if (butterMorphEnabled)
                {
                    services.AddKrackendOrchestrationsRuntimeButterMorph();
                }

                if (gossipEnabled)
                {
                    runtime.AddRedisGossip(context.Configuration);
                }

                runtime.AddMule(
                    mule =>
                    {
                        mule.UseEntityFrameworkCore<RuntimeDbContext>();
                        mule.UseFastLaneRedis(options =>
                        {
                            options.ConnectionString = infrastructure.RedisConnectionString;
                            options.KeyPrefix = $"krackend:e2e:{resolvedDatabaseName}";
                            options.IntentFlushSize = 100;
                            options.CompletionFlushSize = 100;
                            options.FlushInterval = TimeSpan.FromMilliseconds(25);
                            options.LeaseDuration = TimeSpan.FromSeconds(30);
                            options.DeduplicationRetention = TimeSpan.FromMinutes(30);
                        });
                        mule.Configure(options =>
                        {
                            options.ImmediateDispatch = true;
                            options.RecoveryMode = MuleRecoveryMode.Polling;
                            options.DispatchInterval = TimeSpan.FromMilliseconds(200);
                            options.DispatchBatchSize = 100;
                            options.DispatchQueueCapacity = 0;
                            options.ExecutionQueueCapacity = 0;
                            options.WorkerCount = 16;
                            options.MaxDegreeOfParallelism = 16;
                            options.MaxDrainBatchesPerCycle = int.MaxValue;
                            options.MaxDrainActionsPerCycle = 0;
                            options.DrainUntilEmpty = true;
                        });
                    },
                    runtimeMule =>
                    {
                        runtimeMule.ArtifactLifecycleWorkerCount = 8;
                        runtimeMule.ArtifactLifecycleMaxDegreeOfParallelism = 8;
                    });
            })
            .Build();

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
            await EnsureRuntimeDatabaseCreatedAsync(dbContext);
        }

        await WaitForRuntimeDatabaseAsync(connectionString);

        await host.StartAsync();
        return new RealMessagingRuntimeHarness(host, resolvedDatabaseName, connectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    private static bool IsDatabaseAlreadyCreated(SqlException exception)
        => exception.Errors.Cast<SqlError>().Any(error => error.Number == 1801);

    private static async Task EnsureRuntimeDatabaseCreatedAsync(RuntimeDbContext dbContext)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(1);
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await dbContext.Database.EnsureCreatedAsync();
                return;
            }
            catch (SqlException exception) when (IsDatabaseAlreadyCreated(exception))
            {
                // SQL Server can report a freshly restarted database as missing and then reject CREATE DATABASE.
                // The chaos tests intentionally restart SQL and reconnect to the same database.
                return;
            }
            catch (Exception exception) when (IsTransientDatabaseAvailabilityFailure(exception))
            {
                lastError = exception;
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        throw new TimeoutException("Runtime test database did not become available for creation.", lastError);
    }

    private static async Task WaitForRuntimeDatabaseAsync(string connectionString)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(1);
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                await using var command = new SqlCommand("SELECT 1", connection);
                await command.ExecuteScalarAsync();
                return;
            }
            catch (Exception exception) when (IsTransientDatabaseAvailabilityFailure(exception))
            {
                lastError = exception;
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        throw new TimeoutException("Runtime test database did not become queryable.", lastError);
    }

    private static bool IsTransientDatabaseAvailabilityFailure(Exception exception)
    {
        var sqlException = FindSqlException(exception);
        return sqlException is not null &&
            sqlException.Errors.Cast<SqlError>().Any(error => error.Number is 4060 or 18456 or 233 or 64 or -2);
    }

    private static SqlException? FindSqlException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current is SqlException sqlException)
            {
                return sqlException;
            }
        }

        return null;
    }

    public async Task<RuntimeArtifactDeploymentResult> DeployAndWaitReadyAsync(
        RuntimeArtifactDeliveryPackage package,
        CancellationToken cancellationToken = default)
    {
        await using var scope = Services.CreateAsyncScope();
        var deployment = await scope.ServiceProvider
            .GetRequiredService<IRuntimeArtifactDeploymentService>()
            .DeployAsync(package, "real-infra-e2e", cancellationToken);

        Assert.True(deployment.Accepted, deployment.Message);
        await WaitForArtifactReadyAsync(deployment.RuntimeArtifactId);
        return deployment;
    }

    public async Task PublishTriggerAsync(
        string topic,
        string version,
        JsonNode payload,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = Services.CreateAsyncScope();
        var metadataSetter = scope.ServiceProvider.GetRequiredService<IOrchestrationMessageMetadataSetter>();
        metadataSetter.Set(new OrchestrationMessageMetadata
        {
            CorrelationId = correlationId
        });

        var producer = scope.ServiceProvider.GetRequiredService<IProducer>();
        await producer.PublishAsync(payload, topic, PigeonVersion.Parse(version), cancellationToken);
    }

    public async Task PublishBackchannelAsync(
        OrchestrationReplyAddress replyAddress,
        OrchestrationMessageMetadata messageMetadata,
        OrchestrationExecutionResultMetadata resultMetadata,
        JsonNode? payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replyAddress);
        ArgumentNullException.ThrowIfNull(messageMetadata);
        ArgumentNullException.ThrowIfNull(resultMetadata);

        var settings = JsonNode.Parse(replyAddress.SettingsPayload)!.AsObject();
        var topic = settings["topic"]?.GetValue<string>() ?? settings["Topic"]?.GetValue<string>();
        var version = settings["version"]?.GetValue<string>() ?? settings["Version"]?.GetValue<string>();

        await using var scope = Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IOrchestrationMessageMetadataSetter>().Set(messageMetadata);
        scope.ServiceProvider.GetRequiredService<IOrchestrationExecutionResultMetadataSetter>().Set(resultMetadata);

        var producer = scope.ServiceProvider.GetRequiredService<IProducer>();
        await producer.PublishAsync(payload ?? new JsonObject(), topic!, PigeonVersion.Parse(version!), cancellationToken);
    }

    public async Task<T> QueryAsync<T>(Func<RuntimeDbContext, Task<T>> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
        return await query(dbContext);
    }

    public async Task<RuntimeOrchestrationArtifact> WaitForArtifactReadyAsync(string artifactId)
    {
        var readiness = await PollingAssert.EventuallyAsync(
            async () =>
            {
                await using var scope = Services.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>();
                var artifact = await repository.GetById(ParseId(artifactId));
                var localState = scope.ServiceProvider.GetRequiredService<IRuntimeIngressLocalState>();
                return new RealMessagingArtifactReadiness(artifact, localState.IsApplied(artifactId, artifact.IngressGeneration));
            },
            readiness => readiness.Artifact.Status == RuntimeOrchestrationArtifactStatus.Ready && readiness.AppliedLocally,
            $"Runtime artifact '{artifactId}' was not projected and stood up",
            TimeSpan.FromSeconds(60));

        return readiness.Artifact;
    }

    public async Task<OrchestrationInstance> WaitForInstanceStatusAsync(
        string correlationId,
        OrchestrationInstanceStatus status,
        TimeSpan? timeout = null)
    {
        var readiness = await PollingAssert.EventuallyAsync(
            () => QueryAsync(async dbContext =>
            {
                var instances = await dbContext.OrchestrationInstances
                    .AsNoTracking()
                    .OrderByDescending(instance => instance.StartedOnUtc)
                    .ToArrayAsync();
                var matching = instances.FirstOrDefault(instance => instance.CorrelationId == correlationId);
                var observed = string.Join(
                    "; ",
                    instances.Select(instance => $"{instance.CorrelationId}:{instance.Status}:{instance.OrchestrationDefinitionKey}"));

                return new RealMessagingInstanceReadiness(matching, instances.Length, observed);
            }),
            readiness => readiness.MatchingInstance?.Status == status,
            $"Orchestration instance with correlation '{correlationId}' did not reach status '{status}'",
            timeout ?? TimeSpan.FromSeconds(60));

        return readiness.MatchingInstance!;
    }

    private static Id ParseId(string value)
        => new(Ulid.Parse(value));
}
