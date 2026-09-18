namespace Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Errors;
using Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon;
using Krackend.Sagas.Orchestrations.Client.Operations;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Management;
using Pigeon.Messaging.Rabbit;
using Pigeon.Messaging.Topology;
using PigeonVersion = Pigeon.Messaging.Contracts.SemanticVersion;

internal sealed class RealMessagingServiceHost : IAsyncDisposable
{
    private readonly IHost _host;

    private RealMessagingServiceHost(IHost host, RealMessagingScenario scenario)
    {
        _host = host;
        Scenario = scenario;
    }

    public RealMessagingScenario Scenario { get; }

    public static async Task<RealMessagingServiceHost> StartAsync(
        RuntimeRealInfrastructureFixture infrastructure,
        IReadOnlyCollection<RealMessagingConsumerEndpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(infrastructure);
        ArgumentNullException.ThrowIfNull(endpoints);

        var scenario = new RealMessagingScenario();
        var serviceDomain = $"Krackend.RealMessagingServiceHost.{Guid.NewGuid():N}";
        var settings = new Dictionary<string, string?>
        {
            ["Pigeon:Domain"] = serviceDomain,
            ["Pigeon:MessageBrokers:RabbitMq:Url"] = infrastructure.RabbitMqConnectionString,
            ["Pigeon:MessageBrokers:RabbitMq:ConnectionString"] = infrastructure.RabbitMqConnectionString
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
                services.AddSingleton(scenario);
                services
                    .AddKrackendOrchestrationsClient(ConfigureErrorMappings)
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
            })
            .Build();

        var consumingConfigurator = host.Services.GetRequiredService<IConsumingConfigurator>();
        var topologyProvisioning = host.Services.GetRequiredService<ITopologyProvisioningService>();
        foreach (var endpoint in endpoints)
        {
            consumingConfigurator.AddConsumer<JsonNode>(
                endpoint.Topic,
                PigeonVersion.Parse(endpoint.Version),
                "Default",
                async (consumeContext, message) =>
                {
                    await HandleMessageAsync(
                        consumeContext.Services,
                        endpoint.Topic,
                        message);
                });
        }

        await topologyProvisioning.EnsureStartupTopologyAsync(CancellationToken.None);

        await host.StartAsync();
        return new RealMessagingServiceHost(host, scenario);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    private static async Task HandleMessageAsync(
        IServiceProvider services,
        string topic,
        JsonNode? message)
    {
        var scenario = services.GetRequiredService<RealMessagingScenario>();
        var metadataAccessor = services.GetRequiredService<IOrchestrationMessageMetadataAccessor>();
        var metadata = metadataAccessor.Get() ?? new OrchestrationMessageMetadata();
        scenario.Record(topic, message, metadata);

        var outcome = scenario.NextOutcome(topic);
        if (outcome.Delay > TimeSpan.Zero)
        {
            await Task.Delay(outcome.Delay);
        }

        if (outcome.Kind == RealMessagingServiceOutcomeKind.NoReply)
        {
            return;
        }

        var client = services.GetRequiredService<IOrchestrationOperationClient>();
        var options = new OrchestrationOperationOptions
        {
            ServiceName = "real-e2e-service",
            OperationName = topic
        };

        client.Begin<JsonNode>();
        try
        {
            if (outcome.Kind == RealMessagingServiceOutcomeKind.Success)
            {
                await client.ReportSuccessAsync<JsonNode, JsonNode>(
                    outcome.Payload ?? new JsonObject(),
                    options);
                return;
            }

            await client.ReportFailureAsync<JsonNode>(
                new RealMessagingServiceException(
                    outcome.ErrorCode ?? "UnhandledServiceFailure",
                    outcome.ErrorMessage ?? "Service failed.",
                    outcome.IsRetryableCandidate),
                options);
        }
        finally
        {
            client.Close();
        }
    }

    private static void ConfigureErrorMappings(OrchestrationClientErrorMappingOptions options)
    {
        options.Map<RealMessagingServiceException>(
            "InventoryTransientFailure",
            exception => exception.ErrorCode == "InventoryTransientFailure",
            isRetryableCandidate: true);
        options.Map<RealMessagingServiceException>(
            "PaymentTemporaryFailure",
            exception => exception.ErrorCode == "PaymentTemporaryFailure",
            isRetryableCandidate: true);
        options.Map<RealMessagingServiceException>(
            "PaymentRejected",
            exception => exception.ErrorCode == "PaymentRejected",
            isRetryableCandidate: false);
        options.Map<RealMessagingServiceException>(
            "RiskTemporaryFailure",
            exception => exception.ErrorCode == "RiskTemporaryFailure",
            isRetryableCandidate: true);
        options.Map<RealMessagingServiceException>(
            "PermanentFailure",
            exception => exception.ErrorCode == "PermanentFailure",
            isRetryableCandidate: false);
        options.Map<RealMessagingServiceException>(
            "TransientFailure",
            exception => exception.ErrorCode == "TransientFailure",
            isRetryableCandidate: true);
    }
}
