namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Support;

using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json.Nodes;

internal sealed class RuntimeAcceptanceHarness : IDisposable
{
    private readonly IServiceScope _scope;

    private RuntimeAcceptanceHarness(
        ServiceProvider provider,
        IServiceScope scope,
        RecordingMessagingIngressAdapter ingressAdapter)
    {
        Provider = provider;
        _scope = scope;
        IngressAdapter = ingressAdapter;
    }

    public ServiceProvider Provider { get; }

    public IServiceProvider Services => _scope.ServiceProvider;

    public RecordingMessagingIngressAdapter IngressAdapter { get; }

    public RecordingRemoteCommandDispatcher Dispatcher
        => (RecordingRemoteCommandDispatcher)Services.GetRequiredService<IRemoteCommandDispatcher>();

    public static RuntimeAcceptanceHarness Create()
    {
        var ingressAdapter = new RecordingMessagingIngressAdapter();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddKrackendOrchestrationsRuntime();
        services.Replace(ServiceDescriptor.Scoped<IRemoteCommandDispatcher, RecordingRemoteCommandDispatcher>());
        services.Replace(ServiceDescriptor.Scoped<IGetAllIngressConfigurationsAccessor, RepositoryBackedIngressConfigurationAccessor>());
        services.Replace(ServiceDescriptor.Scoped<IGetIngressConfigurationByArtifactAccessor, RepositoryBackedIngressConfigurationAccessor>());
        services.Replace(ServiceDescriptor.Singleton<IMessagingIngressAdapter>(ingressAdapter));

        var provider = services.BuildServiceProvider();
        return new RuntimeAcceptanceHarness(provider, provider.CreateScope(), ingressAdapter);
    }

    public Task<RuntimeArtifactDeploymentResult> DeployAsync(RuntimeArtifactDeliveryPackage package)
        => Services.GetRequiredService<IRuntimeArtifactDeploymentService>().DeployAsync(package, "acceptance-tests");

    public async Task StartFromMessagingIngressAsync(
        string topic,
        string version,
        JsonNode payload,
        string correlationId)
    {
        using var ingressScope = Provider.CreateScope();
        using var reader = ingressScope.ServiceProvider.GetRequiredService<IGetAllIngressConfigurationsAccessor>();
        var dataset = await reader.ReadAsync();
        var selector = ingressScope.ServiceProvider.GetRequiredService<IMessagingIngressConfigurationSelector>();
        var configuration = selector.SelectMatching(dataset.Configurations, topic, version)
            .Single(x => x.IngressKind == IngressKind.Trigger);

        await Services.GetRequiredService<ISagaEngine>().StartOrchestrationAsync(new StartIntent
        {
            ArtifactId = configuration.ArtifactId,
            IngressTransport = IngressTransport.Messaging,
            MessageMetadata = new OrchestrationMessageMetadata
            {
                CorrelationId = correlationId
            },
            Payload = payload
        });
    }

    public Task ForwardAsync(
        RemoteCommand command,
        JsonNode? payload,
        OrchestrationExecutionResultMetadata resultMetadata)
        => ForwardCoreAsync(command, payload, resultMetadata);

    private async Task ForwardCoreAsync(
        RemoteCommand command,
        JsonNode? payload,
        OrchestrationExecutionResultMetadata resultMetadata)
    {
        var instance = await Services
            .GetRequiredService<IOrchestrationInstanceRepository>()
            .GetById(new Krackend.Sagas.Orchestrations.Abstractions.Primitives.Id(
                Ulid.Parse(command.OrchestrationInstanceId)));

        await Services.GetRequiredService<ISagaEngine>().OrchestrateAsync(new ForwardIntent
        {
            ArtifactId = instance.RuntimeOrchestrationArtifactId.ToString(),
            IngressTransport = IngressTransport.Messaging,
            MessageMetadata = command.MessageMetadata,
            ExecutionResultMetadata = resultMetadata,
            Payload = payload
        });
    }

    public T GetRequiredService<T>()
        where T : notnull
        => Services.GetRequiredService<T>();

    public void Dispose()
    {
        _scope.Dispose();
        Provider.Dispose();
    }
}
