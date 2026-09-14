namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Support;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json;
using System.Text.Json.Nodes;

internal sealed class MessagingEngineHarness : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IServiceScope _scope;

    private MessagingEngineHarness(
        ServiceProvider provider,
        IServiceScope scope,
        Id artifactId,
        OrchestrationArtifact artifact)
    {
        Provider = provider;
        _scope = scope;
        ArtifactId = artifactId;
        Artifact = artifact;
    }

    public ServiceProvider Provider { get; }

    public IServiceProvider Services => _scope.ServiceProvider;

    public Id ArtifactId { get; }

    public OrchestrationArtifact Artifact { get; }

    public ISagaEngine Engine => Services.GetRequiredService<ISagaEngine>();

    public RecordingRemoteCommandDispatcher Dispatcher
        => (RecordingRemoteCommandDispatcher)Services.GetRequiredService<IRemoteCommandDispatcher>();

    public static async Task<MessagingEngineHarness> CreateAsync(
        OrchestrationArtifact artifact,
        Action<IServiceCollection>? configureServices = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.Replace(ServiceDescriptor.Scoped<IRemoteCommandDispatcher, RecordingRemoteCommandDispatcher>());
        services.Replace(ServiceDescriptor.Scoped<IGetIngressConfigurationByArtifactAccessor, TestIngressConfigurationAccessor>());
        configureServices?.Invoke(services);

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        var artifactId = await SeedArtifactAsync(scope.ServiceProvider, artifact);
        await SeedBackchannelAsync(scope.ServiceProvider, artifactId, artifact);

        return new MessagingEngineHarness(provider, scope, artifactId, artifact);
    }

    public Task StartAsync(JsonNode payload, string? correlationId = null)
        => Engine.StartOrchestrationAsync(new StartIntent
        {
            ArtifactId = ArtifactId.ToString(),
            IngressTransport = IngressTransport.Messaging,
            MessageMetadata = new OrchestrationMessageMetadata
            {
                CorrelationId = correlationId
            },
            Payload = payload
        });

    public Task ForwardAsync(
        RemoteCommand command,
        JsonNode? payload,
        OrchestrationExecutionResultMetadata? resultMetadata)
        => Engine.OrchestrateAsync(new ForwardIntent
        {
            ArtifactId = ArtifactId.ToString(),
            IngressTransport = IngressTransport.Messaging,
            MessageMetadata = command.MessageMetadata,
            ExecutionResultMetadata = resultMetadata,
            Payload = payload
        });

    public Task<OrchestrationInstance> GetInstanceAsync(RemoteCommand command)
        => Services
            .GetRequiredService<IOrchestrationInstanceRepository>()
            .GetById(ParseId(command.OrchestrationInstanceId));

    public Task<TaskExecution> GetTaskAsync(RemoteCommand command)
        => Services
            .GetRequiredService<ITaskExecutionRepository>()
            .GetById(ParseId(command.TaskExecutionId));

    public Task<TaskExecutionAttempt> GetAttemptAsync(RemoteCommand command)
        => Services
            .GetRequiredService<ITaskExecutionAttemptRepository>()
            .GetById(ParseId(command.TaskExecutionAttemptId));

    public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetAttemptsAsync(RemoteCommand command)
        => Services
            .GetRequiredService<ITaskExecutionAttemptRepository>()
            .GetByTaskExecutionId(ParseId(command.TaskExecutionId));

    public async Task<IReadOnlyCollection<ExecutionTransition>> GetTransitionsAsync(RemoteCommand command)
    {
        var repository = Services.GetRequiredService<IExecutionTransitionRepository>();
        return await repository.GetByInstanceId(ParseId(command.OrchestrationInstanceId));
    }

    public T GetRequiredService<T>()
        where T : notnull
        => Services.GetRequiredService<T>();

    public void Dispose()
    {
        _scope.Dispose();
        Provider.Dispose();
    }

    private static async Task<Id> SeedArtifactAsync(
        IServiceProvider services,
        OrchestrationArtifact artifact)
    {
        var artifactId = Id.New();
        var repository = services.GetRequiredService<IRuntimeArtifactRepository>();
        await repository.Upsert(new RuntimeOrchestrationArtifact
        {
            Id = artifactId,
            OrchestrationDefinitionKey = artifact.Key,
            ArtifactType = "orchestration-version-snapshot",
            SourceOrchestrationVersionId = artifact.OrchestrationVersionId,
            Version = artifact.Version,
            ArtifactChecksum = artifact.Checksum,
            ArtifactPayload = JsonSerializer.SerializeToNode(artifact, SerializerOptions),
            Status = RuntimeOrchestrationArtifactStatus.Ready,
            IngressGeneration = 1,
            IsActive = true,
            DeployedOnUtc = DateTime.UtcNow,
            ActivatedOnUtc = DateTime.UtcNow
        });

        return artifactId;
    }

    private static Task SeedBackchannelAsync(
        IServiceProvider services,
        Id artifactId,
        OrchestrationArtifact artifact)
    {
        var now = DateTime.UtcNow;
        var repository = services.GetRequiredService<IRuntimeIngressConfigurationRepository>();
        return repository.UpsertForArtifactAsync(
            artifactId,
            [
                new RuntimeIngressConfiguration
                {
                    Id = Id.New(),
                    RuntimeOrchestrationArtifactId = artifactId,
                    ConfigurationKey = "backchannel:messaging",
                    IngressKind = IngressKind.Backchannel,
                    IngressTransport = IngressTransport.Messaging,
                    SettingsPayload = $$"""{"topic":"orchestrations.{{artifact.Key}}","version":"{{artifact.Version}}"}""",
                    IsActive = true,
                    CreatedOnUtc = now,
                    UpdatedOnUtc = now
                }
            ]);
    }

    private static Id ParseId(string value)
        => new(Ulid.Parse(value));
}
