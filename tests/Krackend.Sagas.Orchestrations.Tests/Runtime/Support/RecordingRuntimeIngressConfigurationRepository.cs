namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Support;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;

internal sealed class RecordingRuntimeIngressConfigurationRepository : IRuntimeIngressConfigurationRepository
{
    private readonly List<RuntimeIngressConfiguration> _configurations = [];

    public IReadOnlyCollection<RuntimeIngressConfiguration> Configurations => _configurations.ToArray();

    public Task UpsertForArtifactAsync(
        Id runtimeOrchestrationArtifactId,
        IReadOnlyCollection<RuntimeIngressConfiguration> configurations,
        CancellationToken cancellationToken = default)
    {
        _configurations.RemoveAll(configuration =>
            configuration.RuntimeOrchestrationArtifactId == runtimeOrchestrationArtifactId);
        _configurations.AddRange(configurations);
        return Task.CompletedTask;
    }

    public Task DeactivateForArtifactsAsync(
        IReadOnlyCollection<Id> runtimeOrchestrationArtifactIds,
        CancellationToken cancellationToken = default)
    {
        foreach (var configuration in _configurations.Where(configuration =>
                     runtimeOrchestrationArtifactIds.Contains(configuration.RuntimeOrchestrationArtifactId)))
        {
            configuration.IsActive = false;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<RuntimeIngressConfiguration>> ReadActiveAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<RuntimeIngressConfiguration>>(
            _configurations
                .Where(configuration => configuration.IsActive)
                .Skip(skip)
                .Take(take)
                .ToArray());

    public Task<IReadOnlyCollection<RuntimeIngressConfiguration>> GetActiveByArtifactIdAsync(
        Id runtimeOrchestrationArtifactId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<RuntimeIngressConfiguration>>(
            _configurations
                .Where(configuration =>
                    configuration.IsActive &&
                    configuration.RuntimeOrchestrationArtifactId == runtimeOrchestrationArtifactId)
                .ToArray());
}
