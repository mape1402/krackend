using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using DesignSchemaContractSnapshot = Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.SchemaContractSnapshot;
using RegistrySchemaContractSnapshot = Krackend.Sagas.Orchestrations.SchemaRegistry.SchemaContractSnapshot;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Default schema binding snapshot resolver used during artifact publication.
/// </summary>
public sealed class OrchestrationSchemaBindingSnapshotResolver : IOrchestrationSchemaBindingSnapshotResolver
{
    private readonly ISchemaContractResolverCatalog _resolverCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationSchemaBindingSnapshotResolver"/> class.
    /// </summary>
    /// <param name="resolverCatalog">Schema resolver catalog.</param>
    public OrchestrationSchemaBindingSnapshotResolver(ISchemaContractResolverCatalog resolverCatalog)
    {
        _resolverCatalog = resolverCatalog ?? throw new ArgumentNullException(nameof(resolverCatalog));
    }

    /// <inheritdoc />
    public async Task ResolveAsync(
        OrchestrationVersion version,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);

        foreach (var trigger in version.TriggerBindings.Where(x => x.IsEnabled))
        {
            await ResolveTriggerAsync(trigger, cancellationToken);
        }

        foreach (var stage in version.StageDefinitions)
        {
            foreach (var task in stage.TaskDefinitions.Where(x => x.IsEnabled))
            {
                await ResolveTaskAsync(task, cancellationToken);
                await ResolveCompensationAsync(task.CompensationDefinition, cancellationToken);
            }
        }
    }

    private async Task ResolveTriggerAsync(
        TriggerBinding trigger,
        CancellationToken cancellationToken)
    {
        if (trigger.TriggerChannel is EventTriggerChannel eventChannel)
        {
            await ResolveBindingAsync(
                eventChannel.SchemaBinding,
                SchemaContractKind.Event,
                cancellationToken);
        }
    }

    private async Task ResolveTaskAsync(
        TaskDefinition task,
        CancellationToken cancellationToken)
    {
        if (task.Configuration is not MessagingTaskConfiguration messaging)
        {
            return;
        }

        if (IsCommandReference(messaging.SchemaBinding))
        {
            await ResolveCommandReferenceAsync(messaging, cancellationToken);
            return;
        }

        await ResolveBindingAsync(
            messaging.RequestSchemaBinding ?? messaging.SchemaBinding,
            SchemaContractKind.CommandRequest,
            cancellationToken);
        await ResolveBindingAsync(
            messaging.ResponseSchemaBinding,
            SchemaContractKind.CommandResponse,
            cancellationToken);
    }

    private async Task ResolveCompensationAsync(
        CompensationDefinition compensation,
        CancellationToken cancellationToken)
    {
        if (compensation?.Configuration is not MessagingTaskConfiguration messaging)
        {
            return;
        }

        if (IsCommandReference(messaging.SchemaBinding))
        {
            await ResolveCommandReferenceAsync(messaging, cancellationToken);
            return;
        }

        await ResolveBindingAsync(
            messaging.RequestSchemaBinding ?? messaging.SchemaBinding,
            SchemaContractKind.CommandRequest,
            cancellationToken);
        await ResolveBindingAsync(
            messaging.ResponseSchemaBinding,
            SchemaContractKind.CommandResponse,
            cancellationToken);
    }

    private async Task ResolveCommandReferenceAsync(
        MessagingTaskConfiguration messaging,
        CancellationToken cancellationToken)
    {
        var commandBinding = messaging.SchemaBinding;
        messaging.RequestSchemaBinding = CreateCommandSideBinding(
            commandBinding,
            SchemaContractKind.CommandRequest,
            commandBinding.IsValidationEnabled || messaging.HasRequestValidation || messaging.HasSchemaValidation,
            commandBinding.StrictMode);
        messaging.ResponseSchemaBinding = CreateCommandSideBinding(
            commandBinding,
            SchemaContractKind.CommandResponse,
            messaging.HasResponseValidation,
            messaging.HasResponseValidation && commandBinding.StrictMode);

        await ResolveBindingAsync(
            messaging.RequestSchemaBinding,
            SchemaContractKind.CommandRequest,
            cancellationToken);
        await ResolveBindingAsync(
            messaging.ResponseSchemaBinding,
            SchemaContractKind.CommandResponse,
            cancellationToken);
    }

    private async Task ResolveBindingAsync(
        SchemaBinding binding,
        SchemaContractKind contractKind,
        CancellationToken cancellationToken)
    {
        if (binding is null)
        {
            return;
        }

        var effectiveKind = contractKind == SchemaContractKind.Unspecified ? binding.ContractKind : contractKind;
        if (HasCurrentSnapshot(binding, effectiveKind))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(binding.RegistryProviderKey))
        {
            if (binding.StrictMode || binding.IsValidationEnabled)
            {
                throw new SchemaRegistryException(
                    $"Schema contract '{binding.ContractKey}' v{binding.ContractVersion} does not define a schema registry provider key.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(binding.ContractKey))
        {
            return;
        }

        var reference = new SchemaContractReference
        {
            ProviderId = binding.RegistryProviderId.ToString(),
            ProviderKey = binding.RegistryProviderKey,
            ContractId = binding.ContractId.ToString(),
            ContractKey = binding.ContractKey,
            ContractVersion = binding.ContractVersion.ToString(),
            Kind = effectiveKind,
            StrictMode = binding.StrictMode,
            IsValidationEnabled = binding.IsValidationEnabled
        };
        var resolver = _resolverCatalog.GetResolver(reference);
        var result = await resolver.ResolveAsync(
            new SchemaContractResolutionRequest
            {
                Reference = reference,
                RequireRemoteResolution = binding.StrictMode || binding.IsValidationEnabled
            },
            cancellationToken);

        if (result is null)
        {
            return;
        }

        if (result.Status == SchemaContractResolutionStatus.Resolved && result.Snapshot is not null)
        {
            binding.Snapshot = MapSnapshot(result.Snapshot);
            return;
        }

        if (MustHaveSnapshot(binding, result.Status))
        {
            throw new SchemaRegistryException(
                $"Schema contract '{binding.ContractKey}' v{binding.ContractVersion} could not be resolved: {result.Message}");
        }
    }

    private static bool IsCommandReference(SchemaBinding binding)
        => binding is not null &&
            binding.ContractKind == SchemaContractKind.Command &&
            !string.IsNullOrWhiteSpace(binding.ContractKey);

    private static SchemaBinding CreateCommandSideBinding(
        SchemaBinding commandBinding,
        SchemaContractKind contractKind,
        bool isValidationEnabled,
        bool strictMode)
        => new()
        {
            Id = commandBinding.Id,
            ElementType = commandBinding.ElementType,
            ElementId = commandBinding.ElementId,
            ContractId = commandBinding.ContractId,
            ContractKey = commandBinding.ContractKey,
            ContractVersion = commandBinding.ContractVersion,
            RegistryProviderId = commandBinding.RegistryProviderId,
            RegistryProviderKey = commandBinding.RegistryProviderKey,
            ContractKind = contractKind,
            StrictMode = strictMode,
            IsValidationEnabled = isValidationEnabled,
            Snapshot = commandBinding.Snapshot?.ContractKind == contractKind
                ? commandBinding.Snapshot
                : null
        };

    private static bool HasCurrentSnapshot(SchemaBinding binding, SchemaContractKind contractKind)
    {
        var snapshot = binding.Snapshot;
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.ContentHash))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(snapshot.RegistryProviderKey) &&
            string.IsNullOrWhiteSpace(snapshot.ContractKey) &&
            string.IsNullOrWhiteSpace(snapshot.ContractVersion))
        {
            return true;
        }

        return snapshot.ContractKind == contractKind &&
            string.Equals(snapshot.RegistryProviderKey, binding.RegistryProviderKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(snapshot.ContractKey, binding.ContractKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(snapshot.ContractVersion, binding.ContractVersion.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MustHaveSnapshot(
        SchemaBinding binding,
        SchemaContractResolutionStatus status)
        => (status is SchemaContractResolutionStatus.NotConfigured or SchemaContractResolutionStatus.Invalid or SchemaContractResolutionStatus.NotFound or SchemaContractResolutionStatus.Unavailable) &&
            (binding.StrictMode || binding.IsValidationEnabled);

    private static DesignSchemaContractSnapshot MapSnapshot(RegistrySchemaContractSnapshot snapshot)
        => new()
        {
            ContractKind = snapshot.Reference.Kind,
            RegistryProviderId = snapshot.Reference.ProviderId,
            RegistryProviderKey = snapshot.Reference.ProviderKey,
            ContractId = snapshot.Reference.ContractId,
            ContractKey = snapshot.Reference.ContractKey,
            ContractVersion = snapshot.Reference.ContractVersion,
            SchemaFormat = snapshot.SchemaFormat,
            SchemaJson = snapshot.SchemaJson,
            ContentHash = snapshot.ContentHash,
            SourceArtifactId = snapshot.SourceArtifactId,
            ResolvedBy = snapshot.ResolvedBy,
            ResolvedAtUtc = snapshot.ResolvedAtUtc
        };
}
