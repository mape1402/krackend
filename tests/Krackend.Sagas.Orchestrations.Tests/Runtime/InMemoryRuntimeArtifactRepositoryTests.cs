namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Microsoft.Extensions.DependencyInjection;

public sealed class InMemoryRuntimeArtifactRepositoryTests
{
    [Fact]
    public async Task ProjectionStateMethodsOnlyUpdateMatchingGeneration()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>();
        var artifact = RuntimeArtifact(new SemanticVersion(1, 0, 0), RuntimeOrchestrationArtifactStatus.Failed, ingressGeneration: 7);
        artifact.ProjectionError = "previous";
        await repository.Upsert(artifact);

        await repository.MarkProjectionStarted(artifact.Id, ingressGeneration: 6);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Failed, artifact.Status);
        Assert.Equal("previous", artifact.ProjectionError);

        await repository.MarkProjectionStarted(artifact.Id, ingressGeneration: 7);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Pending, artifact.Status);
        Assert.Null(artifact.ProjectionError);
        Assert.NotNull(artifact.ProjectionStartedOnUtc);

        await repository.MarkReady(artifact.Id, ingressGeneration: 7);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Ready, artifact.Status);
        Assert.NotNull(artifact.ActivatedOnUtc);
        Assert.NotNull(artifact.ProjectionCompletedOnUtc);

        await repository.MarkProjectionFailed(artifact.Id, ingressGeneration: 7, "projection failed");
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Failed, artifact.Status);
        Assert.Equal("projection failed", artifact.ProjectionError);
        Assert.NotNull(artifact.ProjectionFailedOnUtc);
    }

    [Fact]
    public async Task QueriesReturnReadyAndActiveArtifactsByKeyAndVersion()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>();
        var ready = RuntimeArtifact(new SemanticVersion(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        var pending = RuntimeArtifact(new SemanticVersion(1, 1, 0), RuntimeOrchestrationArtifactStatus.Pending);
        var inactive = RuntimeArtifact(new SemanticVersion(1, 2, 0), RuntimeOrchestrationArtifactStatus.Ready);
        inactive.IsActive = false;
        await repository.Upsert(ready);
        await repository.Upsert(pending);
        await repository.Upsert(inactive);

        var all = await repository.GetAll();
        var readyArtifacts = await repository.GetReady();
        var active = await repository.GetActive("sales.sale.created");
        var byVersion = await repository.GetByVersion("sales.sale.created", new SemanticVersion(1, 1, 0));

        Assert.Equal(3, all.Count);
        Assert.Single(readyArtifacts);
        Assert.Equal(ready.Id, active.Id);
        Assert.Equal(pending.Id, byVersion.Id);
    }

    [Fact]
    public async Task DeactivateActiveArtifactsRetiresOtherArtifactsAndTheirIngressConfigurations()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var artifactRepository = services.GetRequiredService<IRuntimeArtifactRepository>();
        var ingressRepository = services.GetRequiredService<IRuntimeIngressConfigurationRepository>();
        var oldArtifact = RuntimeArtifact(new SemanticVersion(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        var newArtifact = RuntimeArtifact(new SemanticVersion(1, 1, 0), RuntimeOrchestrationArtifactStatus.Ready);
        await artifactRepository.Upsert(oldArtifact);
        await artifactRepository.Upsert(newArtifact);
        await ingressRepository.UpsertForArtifactAsync(oldArtifact.Id, [Ingress(oldArtifact.Id, "trigger")]);
        await ingressRepository.UpsertForArtifactAsync(newArtifact.Id, [Ingress(newArtifact.Id, "trigger")]);

        await artifactRepository.DeactivateActiveArtifacts("sales.sale.created", newArtifact.Id);

        Assert.False(oldArtifact.IsActive);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Retired, oldArtifact.Status);
        Assert.NotNull(oldArtifact.RetiredOnUtc);
        Assert.Empty(await ingressRepository.GetActiveByArtifactIdAsync(oldArtifact.Id));
        Assert.Single(await ingressRepository.GetActiveByArtifactIdAsync(newArtifact.Id));
    }

    [Fact]
    public async Task IngressRepositoryUpsertsByConfigurationKeyAndReadsOnlyReadyArtifacts()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var artifactRepository = services.GetRequiredService<IRuntimeArtifactRepository>();
        var ingressRepository = services.GetRequiredService<IRuntimeIngressConfigurationRepository>();
        var readyArtifact = RuntimeArtifact(new SemanticVersion(1, 0, 0), RuntimeOrchestrationArtifactStatus.Ready);
        var pendingArtifact = RuntimeArtifact(new SemanticVersion(1, 1, 0), RuntimeOrchestrationArtifactStatus.Pending);
        await artifactRepository.Upsert(readyArtifact);
        await artifactRepository.Upsert(pendingArtifact);

        await ingressRepository.UpsertForArtifactAsync(readyArtifact.Id, [Ingress(readyArtifact.Id, "trigger", """{"topic":"v1"}""")]);
        await ingressRepository.UpsertForArtifactAsync(readyArtifact.Id, [Ingress(readyArtifact.Id, "trigger", """{"topic":"v1-updated"}""")]);
        await ingressRepository.UpsertForArtifactAsync(pendingArtifact.Id, [Ingress(pendingArtifact.Id, "trigger")]);

        var readyIngresses = await ingressRepository.GetActiveByArtifactIdAsync(readyArtifact.Id);
        var activePage = await ingressRepository.ReadActiveAsync(0, 10);

        var readyIngress = Assert.Single(readyIngresses);
        Assert.Equal("""{"topic":"v1-updated"}""", readyIngress.SettingsPayload);
        Assert.Single(activePage);
        Assert.Equal(readyArtifact.Id, activePage.Single().RuntimeOrchestrationArtifactId);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        return services.BuildServiceProvider();
    }

    private static RuntimeOrchestrationArtifact RuntimeArtifact(
        SemanticVersion version,
        RuntimeOrchestrationArtifactStatus status,
        long ingressGeneration = 1)
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            ArtifactType = "orchestration-version-snapshot",
            SourceOrchestrationVersionId = Id.New(),
            Version = version,
            ArtifactChecksum = new Checksum($"checksum-{version}"),
            ArtifactPayload = JsonNode.Parse("{}")!,
            Status = status,
            IngressGeneration = ingressGeneration,
            IsActive = true,
            DeployedOnUtc = DateTime.UtcNow
        };

    private static RuntimeIngressConfiguration Ingress(
        Id artifactId,
        string key,
        string settingsPayload = """{"topic":"events.sales.sale.created"}""")
        => new()
        {
            Id = Id.New(),
            RuntimeOrchestrationArtifactId = artifactId,
            ConfigurationKey = key,
            IngressKind = IngressKind.Trigger,
            IngressTransport = IngressTransport.Messaging,
            SettingsPayload = settingsPayload,
            IsActive = true,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        };
}
