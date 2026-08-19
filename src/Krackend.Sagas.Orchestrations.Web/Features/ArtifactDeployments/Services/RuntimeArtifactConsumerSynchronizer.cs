using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Connects or disconnects trigger and back-channel consumers when runtime artifacts are deployed, deprecated or archived.
/// </summary>
public sealed class RuntimeArtifactConsumerSynchronizer : IRuntimeArtifactConsumerSynchronizer, IRuntimeIngressSynchronizer
{
    private readonly IRuntimeArtifactCatalog _artifactCatalog;
    private readonly IRuntimeArtifactIngressBindingBuilder _bindingBuilder;
    private readonly IReadOnlyCollection<IRuntimeIngressConnector> _connectors;
    private readonly RuntimeIngressSynchronizationOptions _options;
    private readonly ConcurrentDictionary<string, byte> _registeredBindings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeArtifactConsumerSynchronizer"/> class.
    /// </summary>
    /// <param name="artifactCatalog">Runtime artifact catalog.</param>
    /// <param name="bindingBuilder">Runtime ingress binding builder.</param>
    /// <param name="connectors">Available runtime ingress connectors.</param>
    /// <param name="options">Synchronization options.</param>
    public RuntimeArtifactConsumerSynchronizer(
        IRuntimeArtifactCatalog artifactCatalog,
        IRuntimeArtifactIngressBindingBuilder bindingBuilder,
        IEnumerable<IRuntimeIngressConnector> connectors,
        IOptions<RuntimeIngressSynchronizationOptions> options = null)
    {
        _artifactCatalog = artifactCatalog ?? throw new ArgumentNullException(nameof(artifactCatalog));
        _bindingBuilder = bindingBuilder ?? throw new ArgumentNullException(nameof(bindingBuilder));
        _connectors = connectors?.ToArray() ?? Array.Empty<IRuntimeIngressConnector>();
        _options = options?.Value ?? new RuntimeIngressSynchronizationOptions();
    }

    /// <inheritdoc/>
    public async Task Synchronize(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default)
        => await SynchronizeArtifact(artifact, cancellationToken);

    /// <inheritdoc/>
    public async Task SynchronizeActiveArtifacts(CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Max(1, _options.ActiveArtifactPageSize);
        var cursor = RuntimeArtifactPageCursor.First;

        while (cursor is not null)
        {
            var page = await _artifactCatalog.ReadActiveDeployments(cursor, pageSize, cancellationToken);
            foreach (var artifact in page.Items)
                await SynchronizeArtifact(artifact, cancellationToken);

            cursor = page.HasMore ? page.NextCursor : null;
        }
    }

    /// <inheritdoc/>
    public async Task SynchronizeArtifact(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default)
    {
        if (artifact is null || _connectors.Count == 0)
            return;

        var bindings = _bindingBuilder.Build(artifact);

        await ApplyLifecycle(new RuntimeConsumerSyncContext { Artifact = artifact, Bindings = bindings }, cancellationToken);
    }

    private async Task ApplyLifecycle(RuntimeConsumerSyncContext context, CancellationToken cancellationToken)
    {
        var lifecycle = GetLifecycle(context.Artifact.ArtifactType);
        if (lifecycle == RuntimeArtifactLifecycle.Deploy)
        {
            await RegisterDeployConsumers(context, cancellationToken);
            return;
        }

        if (lifecycle == RuntimeArtifactLifecycle.Deprecated)
        {
            await RemoveTriggerConsumers(context, cancellationToken);
            return;
        }

        await RemoveTriggerConsumers(context, cancellationToken);
        UnmarkRegistered(context.Artifact, context.Bindings.BackChannel);
        await RemoveBinding(context, context.Bindings.BackChannel, cancellationToken);
    }

    private async Task RegisterDeployConsumers(RuntimeConsumerSyncContext context, CancellationToken cancellationToken)
    {
        foreach (var trigger in context.Bindings.MessagingTriggers)
            await RegisterTriggerConsumer(context, trigger, cancellationToken);

        await RegisterBackChannelConsumer(context, cancellationToken);
    }

    private async Task RegisterTriggerConsumer(RuntimeConsumerSyncContext context, RuntimeMessagingIngressBinding trigger, CancellationToken cancellationToken)
    {
        if (!TryMarkRegistered(context.Artifact, trigger))
            return;

        await RegisterBinding(context, trigger, cancellationToken);
    }

    private async Task RegisterBackChannelConsumer(RuntimeConsumerSyncContext context, CancellationToken cancellationToken)
    {
        if (!TryMarkRegistered(context.Artifact, context.Bindings.BackChannel))
            return;

        await RegisterBinding(context, context.Bindings.BackChannel, cancellationToken);
    }

    private async Task RemoveTriggerConsumers(RuntimeConsumerSyncContext context, CancellationToken cancellationToken)
    {
        foreach (var trigger in context.Bindings.MessagingTriggers)
        {
            UnmarkRegistered(context.Artifact, trigger);
            await RemoveBinding(context, trigger, cancellationToken);
        }
    }

    private bool TryMarkRegistered(RuntimeOrchestrationArtifact artifact, RuntimeMessagingIngressBinding binding)
        => _registeredBindings.TryAdd(BuildRegistrationKey(artifact, binding), 0);

    private void UnmarkRegistered(RuntimeOrchestrationArtifact artifact, RuntimeMessagingIngressBinding binding)
        => _registeredBindings.TryRemove(BuildRegistrationKey(artifact, binding), out _);

    private static string BuildRegistrationKey(RuntimeOrchestrationArtifact artifact, RuntimeMessagingIngressBinding binding)
        => $"{artifact.Id}::{binding.OrchestrationKey}::{binding.OrchestrationVersion}::{binding.Kind}::{binding.Topic}::{binding.Version}";

    private async Task RegisterBinding(RuntimeConsumerSyncContext context, RuntimeMessagingIngressBinding binding, CancellationToken cancellationToken)
    {
        foreach (var connector in _connectors.Where(x => x.CanHandle(binding)))
            await connector.Connect(context.Artifact, binding, cancellationToken);
    }

    private async Task RemoveBinding(RuntimeConsumerSyncContext context, RuntimeMessagingIngressBinding binding, CancellationToken cancellationToken)
    {
        foreach (var connector in _connectors.Where(x => x.CanHandle(binding)))
            await connector.Disconnect(context.Artifact, binding, cancellationToken);
    }

    private static RuntimeArtifactLifecycle GetLifecycle(string artifactType)
    {
        if (string.Equals(artifactType, "orchestration.deprecate", StringComparison.OrdinalIgnoreCase))
            return RuntimeArtifactLifecycle.Deprecated;

        if (string.Equals(artifactType, "orchestration.archive", StringComparison.OrdinalIgnoreCase))
            return RuntimeArtifactLifecycle.Archived;

        return RuntimeArtifactLifecycle.Deploy;
    }
}
