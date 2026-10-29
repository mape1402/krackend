namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.RetryStrategies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TimeoutBehaviorPolicies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ValidationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using DesignSchemaContractSnapshot = Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.SchemaContractSnapshot;

/// <summary>Default portable orchestration-version package implementation.</summary>
public sealed class OrchestrationVersionPackageService : IOrchestrationVersionPackageService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly IOrchestrationDefinitionRepository _definitionRepository;
    private readonly IOrchestrationVersionRepository _versionRepository;
    private readonly ITriggerBindingRepository _triggerRepository;
    private readonly IVariableDefinitionRepository _variableRepository;
    private readonly IStageRepository _stageRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IParallelGroupRepository _groupRepository;
    private readonly IBranchRuleRepository _branchRepository;
    private readonly IMetadataDescriptorRepository _metadataRepository;
    private readonly IOrchestrationArtifactPayloadFactory _payloadFactory;
    private readonly IControlPlaneUnitOfWork _unitOfWork;

    public OrchestrationVersionPackageService(
        IOrchestrationDefinitionRepository definitionRepository,
        IOrchestrationVersionRepository versionRepository,
        ITriggerBindingRepository triggerRepository,
        IVariableDefinitionRepository variableRepository,
        IStageRepository stageRepository,
        ITaskRepository taskRepository,
        IParallelGroupRepository groupRepository,
        IBranchRuleRepository branchRepository,
        IMetadataDescriptorRepository metadataRepository,
        IOrchestrationArtifactPayloadFactory payloadFactory,
        IControlPlaneUnitOfWork unitOfWork)
    {
        _definitionRepository = definitionRepository ?? throw new ArgumentNullException(nameof(definitionRepository));
        _versionRepository = versionRepository ?? throw new ArgumentNullException(nameof(versionRepository));
        _triggerRepository = triggerRepository ?? throw new ArgumentNullException(nameof(triggerRepository));
        _variableRepository = variableRepository ?? throw new ArgumentNullException(nameof(variableRepository));
        _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _groupRepository = groupRepository ?? throw new ArgumentNullException(nameof(groupRepository));
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _metadataRepository = metadataRepository ?? throw new ArgumentNullException(nameof(metadataRepository));
        _payloadFactory = payloadFactory ?? throw new ArgumentNullException(nameof(payloadFactory));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<OrchestrationVersionPackage> ExportAsync(string orchestrationVersionId, CancellationToken cancellationToken = default)
    {
        var version = await _versionRepository.GetById(PrimitiveParser.ParseId(orchestrationVersionId), cancellationToken)
            ?? throw new InvalidOperationException("The orchestration version was not found.");
        if (version.Status == OrchestrationVersionStatus.Abandoned)
        {
            throw new InvalidOperationException("Abandoned orchestration versions cannot be exported.");
        }

        var definition = await _definitionRepository.GetById(version.OrchestrationDefinitionId, cancellationToken)
            ?? throw new InvalidOperationException("The orchestration definition was not found.");
        await HydrateAsync(version, cancellationToken);

        // Artifact serialization is the canonical, independently versioned graph format. Drafts have no release
        // semantic version yet, so a neutral temporary value is used only for their artifact projection.
        var exportVersion = CopyForPortableArtifact(version);
        var artifactJson = _payloadFactory.CreatePayloadJson(definition, exportVersion);
        return new OrchestrationVersionPackage
        {
            Source = new OrchestrationVersionPackageSource(
                definition.Key,
                version.Id.ToString(),
                version.Version?.ToString() ?? string.Empty,
                version.Status.ToString(),
                version.VersionLabel,
                definition.Name,
                definition.Description,
                string.IsNullOrWhiteSpace(definition.DomainDisplayName) ? definition.Domain : definition.DomainDisplayName,
                string.IsNullOrWhiteSpace(definition.OwnerTeamDisplayName) ? definition.OwnerTeam : definition.OwnerTeamDisplayName),
            ArtifactJson = artifactJson,
            ArtifactChecksum = ComputeChecksum(artifactJson),
            TriggerKeys = version.TriggerBindings.ToDictionary(x => x.Id.ToString(), x => x.Key, StringComparer.Ordinal),
            ParallelGroupNames = version.StageDefinitions.SelectMany(x => x.ParallelGroups)
                .ToDictionary(x => x.Id.ToString(), x => x.Name, StringComparer.Ordinal),
        };
    }

    public OrchestrationVersionPackageInspection Inspect(string packageJson)
    {
        try
        {
            var package = Parse(packageJson);
            var artifact = DeserializeArtifact(package);
            var taskCount = artifact.StageDefinitions.Sum(x => x.TaskDefinitions?.Count ?? 0);
            return new(true, "Package is valid and can be imported as a Draft in the selected orchestration.", artifact.Key,
                artifact.TriggerBindings?.Count ?? 0, artifact.StageDefinitions?.Count ?? 0, taskCount,
                string.IsNullOrWhiteSpace(package.Source.OrchestrationName) ? artifact.Name : package.Source.OrchestrationName,
                package.Source.OrchestrationDescription,
                package.Source.DomainName);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException)
        {
            return new(false, exception.Message);
        }
    }

    public string Serialize(OrchestrationVersionPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        return JsonSerializer.Serialize(package, SerializerOptions);
    }

    public async Task<string> ImportAsync(
        string orchestrationDefinitionId,
        string packageJson,
        string importedBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(importedBy))
        {
            throw new ArgumentException("An importing user is required.", nameof(importedBy));
        }

        var package = Parse(packageJson);
        var artifact = DeserializeArtifact(package);
        var definitionId = PrimitiveParser.ParseId(orchestrationDefinitionId);
        var definition = await _definitionRepository.GetById(definitionId, cancellationToken)
            ?? throw new InvalidOperationException("The destination orchestration was not found.");
        if (!string.Equals(definition.Key, artifact.Key, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The package belongs to a different orchestration key and cannot be imported here.");
        }

        var versions = await _versionRepository.GetAll(definitionId, new PagedSettings(1, 1000, [], []), cancellationToken);
        if (versions.Rows.Any(x => x.Status == OrchestrationVersionStatus.Draft))
        {
            throw new InvalidOperationException("This orchestration already has an active Draft. Finish or abandon it before importing another draft.");
        }

        var targetId = Id.New();
        var target = new OrchestrationVersion
        {
            Id = targetId,
            OrchestrationDefinitionId = definitionId,
            Version = null,
            Status = OrchestrationVersionStatus.Draft,
            VersionLabel = string.Empty,
            Description = string.Empty,
            Checksum = new Checksum($"draft:{targetId}"),
            DraftSlot = $"open:{definitionId}",
            // Preserve the source only as local audit context. It never controls the new Draft's lifecycle.
            Notes = $"Imported from {package.Source.OrchestrationKey} version {package.Source.Version} ({package.Source.Status}); source version id: {package.Source.VersionId}.",
            CreatedOnUtc = DateTime.UtcNow,
            CreatedBy = importedBy,
            ApprovedBy = string.Empty,
            UpdatedBy = string.Empty,
        };

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _versionRepository.Create(target, token);
            await CreatePortableGraphAsync(target, artifact, package, token);
        }, cancellationToken);
        return targetId.ToString();
    }

    private async Task HydrateAsync(OrchestrationVersion version, CancellationToken cancellationToken)
    {
        version.TriggerBindings = (await _triggerRepository.GetAll(version.Id, cancellationToken)).OrderBy(x => x.Key).ToList();
        version.VariableDefinitions = (await _variableRepository.GetAll(version.Id, cancellationToken)).OrderBy(x => x.Key).ToList();
        version.MetadataDescriptors = (await _metadataRepository.GetAllDescriptors(cancellationToken)).OrderBy(x => x.Key).ToList();
        version.StageDefinitions = (await _stageRepository.GetAll(version.Id, cancellationToken)).OrderBy(x => x.Order).ToList();
        foreach (var stage in version.StageDefinitions)
        {
            stage.TaskDefinitions = (await _taskRepository.GetAll(stage.Id, cancellationToken)).OrderBy(x => x.Order).ToList();
            stage.ParallelGroups = (await _groupRepository.GetAll(stage.Id, cancellationToken)).OrderBy(x => x.Name).ToList();
            stage.BranchRules = (await _branchRepository.GetAll(stage.Id, cancellationToken)).ToList();
        }
    }

    private async Task CreatePortableGraphAsync(
        OrchestrationVersion target,
        OrchestrationArtifact artifact,
        OrchestrationVersionPackage package,
        CancellationToken cancellationToken)
    {
        foreach (var variable in artifact.VariableDefinitions ?? [])
        {
            await _variableRepository.Create(new VariableDefinition
            {
                Id = Id.New(), OrchestrationVersionId = target.Id, Key = variable.Key, DisplayName = variable.DisplayName,
                Description = variable.Description, Scope = variable.Scope, ValueType = variable.ValueType,
                DefaultValue = variable.DefaultValue, IsRequired = variable.IsRequired, IsSensitive = variable.IsSensitive,
            }, cancellationToken);
        }

        var triggerIds = new Dictionary<Id, Id>();
        foreach (var trigger in artifact.TriggerBindings ?? [])
        {
            var newId = Id.New();
            triggerIds[trigger.Id] = newId;
            await _triggerRepository.Create(new TriggerBinding
            {
                Id = newId, OrchestrationVersionId = target.Id,
                Key = package.TriggerKeys.TryGetValue(trigger.Id.ToString(), out var key) ? key : $"trigger-{newId}",
                TriggerType = trigger.TriggerType, TriggerChannel = MapTriggerChannel(trigger.TriggerChannel, newId),
                IsEnabled = trigger.IsEnabled, Description = trigger.Description, CompensationDefinition = MapCompensation(trigger.Compensation, newId),
            }, cancellationToken);
        }

        var stageIds = new Dictionary<Id, Id>();
        var taskIds = new Dictionary<Id, Id>();
        var groupIds = new Dictionary<Id, Id>();
        foreach (var stage in artifact.StageDefinitions.OrderBy(x => x.Order))
        {
            var newStageId = Id.New();
            stageIds[stage.Id] = newStageId;
            await _stageRepository.Create(new StageDefinition
            {
                Id = newStageId, OrchestrationVersionId = target.Id, Key = stage.Key, Name = stage.Name,
                Description = stage.Description, Order = stage.Order, IsEnabled = stage.IsEnabled,
                ExecutionCondition = MapCondition(stage.ExecutionCondition), HasExecutionCondition = stage.ExecutionCondition?.IsEnabled == true,
            }, cancellationToken);

            foreach (var group in stage.ParallelGroups ?? [])
            {
                var newGroupId = Id.New();
                groupIds[group.Id] = newGroupId;
                await _groupRepository.Create(new ParallelGroupDefinition
                {
                    Id = newGroupId, StageDefinitionId = newStageId,
                    Name = package.ParallelGroupNames.TryGetValue(group.Id.ToString(), out var groupName) ? groupName : $"parallel-{newGroupId}",
                    JoinPolicy = group.JoinPolicy, MaxParallelAgents = group.MaxParallelAgents,
                }, cancellationToken);
            }

            foreach (var task in (stage.TaskDefinitions ?? []).OrderBy(x => x.Order))
            {
                var newTaskId = Id.New();
                taskIds[task.Id] = newTaskId;
                await _taskRepository.Create(new TaskDefinition
                {
                    Id = newTaskId, StageDefinitionId = newStageId, Key = task.Key, Name = task.Name, Order = task.Order,
                    Notes = task.Notes, Kind = task.Kind, ExecutionMode = task.ExecutionMode,
                    ParallelGroupId = task.ParallelGroupId is { } oldGroup && groupIds.TryGetValue(oldGroup, out var newGroup) ? newGroup : null,
                    ExecutionCondition = MapCondition(task.ExecutionCondition), HasExecutionCondition = task.ExecutionCondition?.IsEnabled == true,
                    Transformation = MapTransformation(task.Transformation), HasTransformation = task.Transformation?.IsEnabled == true,
                    Configuration = MapTaskConfiguration(task.Configuration, newTaskId), RetryPolicy = MapRetryPolicy(task.RetryPolicy),
                    TimeoutPolicy = MapTimeoutPolicy(task.TimeoutPolicy), OnErrorPolicy = task.OnErrorPolicy,
                    CompensationDefinition = MapCompensation(task.Compensation, newTaskId), DispatchType = task.DispatchType, IsEnabled = task.IsEnabled,
                }, cancellationToken);
            }
        }

        foreach (var stage in artifact.StageDefinitions)
        {
            foreach (var rule in stage.BranchRules ?? [])
            {
                await _branchRepository.Create(new BranchRuleDefinition
                {
                    Id = Id.New(), FromType = rule.FromType, FromId = Remap(rule.FromType, rule.FromId, stageIds, taskIds),
                    Condition = MapCondition(rule.Condition), NavigateToType = rule.NavigateToType,
                    NavigateToId = Remap(rule.NavigateToType, rule.NavigateToId, stageIds, taskIds),
                }, cancellationToken);
            }
        }
    }

    private static OrchestrationVersion CopyForPortableArtifact(OrchestrationVersion source) => new()
    {
        Id = source.Id, OrchestrationDefinitionId = source.OrchestrationDefinitionId, Version = source.Version ?? new SemanticVersion(0, 0, 0),
        Status = source.Status, VersionLabel = source.VersionLabel, Description = source.Description, Checksum = source.Checksum,
        TriggerBindings = source.TriggerBindings, VariableDefinitions = source.VariableDefinitions, StageDefinitions = source.StageDefinitions,
        MetadataDescriptors = source.MetadataDescriptors, Notes = source.Notes, CreatedOnUtc = source.CreatedOnUtc, CreatedBy = source.CreatedBy,
    };

    private static OrchestrationVersionPackage Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("Select an orchestration package first.");
        var package = JsonSerializer.Deserialize<OrchestrationVersionPackage>(json, SerializerOptions)
            ?? throw new InvalidOperationException("The selected file is not a Krackend orchestration package.");
        var isCurrentPackage = package.SchemaVersion == OrchestrationVersionPackage.CurrentSchemaVersion && package.PackageKind == OrchestrationVersionPackage.Kind;
        var isLegacyDraftPackage = package.SchemaVersion == 1 && package.PackageKind == "krackend-orchestration-draft";
        if (!isCurrentPackage && !isLegacyDraftPackage)
            throw new InvalidOperationException("This orchestration package schema is not supported.");
        if (string.Equals(package.Source?.Status, OrchestrationVersionStatus.Abandoned.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Abandoned orchestration versions cannot be imported.");
        if (string.IsNullOrWhiteSpace(package.ArtifactJson) || !string.Equals(package.ArtifactChecksum, ComputeChecksum(package.ArtifactJson), StringComparison.Ordinal))
            throw new InvalidOperationException("The package checksum does not match its orchestration payload.");
        return package;
    }

    private static OrchestrationArtifact DeserializeArtifact(OrchestrationVersionPackage package)
    {
        var artifact = JsonSerializer.Deserialize<OrchestrationArtifact>(package.ArtifactJson, SerializerOptions)
            ?? throw new InvalidOperationException("The package does not contain a valid orchestration graph.");
        if (artifact.ArtifactSchemaVersion > OrchestrationArtifactSchemaVersions.Current || string.IsNullOrWhiteSpace(artifact.Key))
            throw new InvalidOperationException("The package artifact schema is not supported.");
        return artifact;
    }

    private static string ComputeChecksum(string value) => $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()}";

    private static Id Remap(ElementType type, Id value, IReadOnlyDictionary<Id, Id> stages, IReadOnlyDictionary<Id, Id> tasks)
        => type switch { ElementType.Stage when stages.TryGetValue(value, out var stage) => stage, ElementType.Task when tasks.TryGetValue(value, out var task) => task, _ => value };

    private static ITriggerChannel MapTriggerChannel(ITriggerChannelArtifact channel, Id triggerId) => channel switch
    {
        EventTriggerChannelArtifact eventChannel => new EventTriggerChannel
        {
            SchemaBinding = MapBinding(eventChannel.SchemaBinding, ElementType.Orchestration, triggerId),
            HasSchemaValidation = eventChannel.SchemaBinding?.IsValidationEnabled == true,
            Validation = MapValidation(eventChannel.Validation), HasValidation = eventChannel.Validation?.IsEnabled == true,
            Topic = eventChannel.Topic, Version = eventChannel.Version,
        },
        _ => throw new InvalidOperationException($"Trigger channel '{channel?.GetType().Name}' is not supported by import."),
    };

    private static ITaskConfiguration MapTaskConfiguration(ITaskConfigurationArtifact configuration, Id taskId) => configuration switch
    {
        MessagingTaskConfigurationArtifact messaging => new MessagingTaskConfiguration
        {
            Topic = messaging.Topic, Version = messaging.Version,
            SchemaBinding = MapBinding(messaging.SchemaBinding, ElementType.Task, taskId),
            RequestSchemaBinding = MapBinding(messaging.RequestSchemaBinding, ElementType.Task, taskId),
            ResponseSchemaBinding = MapBinding(messaging.ResponseSchemaBinding, ElementType.Task, taskId),
            HasSchemaValidation = messaging.RequestSchemaBinding?.IsValidationEnabled == true,
            RequestValidation = MapValidation(messaging.RequestValidation), HasRequestValidation = messaging.RequestValidation?.IsEnabled == true,
            ResponseValidation = MapValidation(messaging.ResponseValidation), HasResponseValidation = messaging.ResponseValidation?.IsEnabled == true,
        },
        HttpTaskConfigurationArtifact http => new HttpTaskConfiguration
        {
            SchemaBinding = MapBinding(http.SchemaBinding, ElementType.Task, taskId), HasSchemaValidation = http.SchemaBinding?.IsValidationEnabled == true,
            BaseUrlVariableRef = http.BaseUrlVariableRef, RelativePath = http.RelativePath, Method = http.Method,
            HeadersTemplate = http.HeadersTemplate?.DeepClone(), QueryTemplate = http.QueryTemplate?.DeepClone(),
            ExpectedStatusCodes = http.ExpectedStatusCodes?.ToList() ?? [], AllowSyncResponse = http.AllowSyncResponse,
        },
        PluginTaskConfigurationArtifact plugin => new PluginTaskConfiguration { PluginId = plugin.PluginId },
        HumanApprovalTaskConfigurationArtifact => new HumanApprovalTaskConfiguration(),
        _ => throw new InvalidOperationException($"Task configuration '{configuration?.GetType().Name}' is not supported by import."),
    };

    private static SchemaBinding MapBinding(SchemaBindingArtifact source, ElementType type, Id elementId)
    {
        if (source is null) return null;
        return new SchemaBinding
        {
            Id = Id.New(), ElementType = type, ElementId = elementId, ContractId = source.ContractId,
            ContractKey = source.ContractKey, ContractVersion = source.ContractVersion, RegistryProviderId = source.RegistryProviderId,
            RegistryProviderKey = source.RegistryProviderKey, ContractKind = source.ContractKind, StrictMode = source.StrictMode,
            IsValidationEnabled = source.IsValidationEnabled, Snapshot = source.Snapshot is null ? null : new DesignSchemaContractSnapshot
            {
                ContractKind = source.Snapshot.ContractKind, RegistryProviderId = source.Snapshot.RegistryProviderId,
                RegistryProviderKey = source.Snapshot.RegistryProviderKey, ContractId = source.Snapshot.ContractId,
                ContractKey = source.Snapshot.ContractKey, ContractVersion = source.Snapshot.ContractVersion,
                SchemaFormat = source.Snapshot.SchemaFormat, SchemaJson = source.Snapshot.SchemaJson, ContentHash = source.Snapshot.ContentHash,
                SourceArtifactId = source.Snapshot.SourceArtifactId, ResolvedBy = source.Snapshot.ResolvedBy, ResolvedAtUtc = source.Snapshot.ResolvedAtUtc,
            },
        };
    }

    private static ExecutionCondition MapCondition(ExecutionConditionArtifact source) => source is null ? null : new ExecutionCondition
    {
        Engine = source.Engine,
        Configuration = source.Configuration switch { DslConditionConfigurationArtifact dsl => new DslConditionConfiguration { Expression = dsl.Expression }, _ => throw new InvalidOperationException("Only DSL conditions are supported by import.") },
    };

    private static TransformationDefinition MapTransformation(TransformationArtifact source) => source is null ? null : new TransformationDefinition
    {
        Engine = source.Engine,
        Configuration = source.Configuration switch { DslTransformationConfigurationArtifact dsl => new DslTransformationConfiguration { Dsl = dsl.Dsl, SourceContextHash = dsl.SourceContextHash, TargetSchemaHash = dsl.TargetSchemaHash, SemanticDiagnosticsJson = dsl.SemanticDiagnosticsJson }, _ => throw new InvalidOperationException("Only DSL transformations are supported by import.") },
    };

    private static ValidationDefinition MapValidation(ValidationArtifact source) => source is null ? null : new ValidationDefinition
    {
        Engine = source.Engine, ErrorCode = source.ErrorCode,
        Configuration = source.Configuration switch { DslValidationConfigurationArtifact dsl => new DslValidationConfiguration { Dsl = dsl.Dsl, SchemaHash = dsl.SchemaHash, SourceContextHash = dsl.SourceContextHash, SemanticDiagnosticsJson = dsl.SemanticDiagnosticsJson }, null => null, _ => throw new InvalidOperationException("Only DSL validations are supported by import.") },
    };

    private static RetryPolicy MapRetryPolicy(RetryPolicyArtifact source) => source is null ? null : new RetryPolicy
    {
        MaxRetries = source.MaxRetries, StrategyType = source.StrategyType, RetryableErrorCodes = source.RetryableErrorCodes?.ToList() ?? [], StopOnNonRetryableError = source.StopOnNonRetryableError,
        Strategy = source.Strategy switch { FixedRetryStrategyArtifact fixedRetry => new FixedRetryStrategy { Delay = fixedRetry.Delay }, _ => throw new InvalidOperationException("Only fixed retry strategies are supported by import.") },
    };

    private static TimeoutPolicy MapTimeoutPolicy(TimeoutPolicyArtifact source) => source is null ? null : new TimeoutPolicy
    {
        Timeout = source.Timeout, TimeoutBehavior = source.TimeoutBehavior,
        TimeoutBehaviorPolicy = source.TimeoutBehaviorPolicy switch
        {
            FailTimeoutBehaviorPolicyArtifact fail => new FailTimeoutBehaviorPolicy { ErrorCode = fail.ErrorCode },
            WaitTimeoutBehaviorPolicyArtifact wait => new WaitTimeoutBehaviorPolicy { OrchestrationAction = wait.OrchestrationAction, WaitingTime = wait.WaitingTime },
            ReconcileTimeoutBehaviorPolicyArtifact reconcile => new ReconcileTimeoutBehaviorPolicy { OrchestrationAction = reconcile.OrchestrationAction, RetryPolicy = MapRetryPolicy(reconcile.RetryPolicy) },
            _ => throw new InvalidOperationException("The timeout policy is not supported by import."),
        },
    };

    private static CompensationDefinition MapCompensation(CompensationArtifact source, Id taskId) => source is null ? null : new CompensationDefinition
    {
        CompensationTaskKind = source.CompensationTaskKind, Transformation = MapTransformation(source.Transformation), HasTransformation = source.Transformation?.IsEnabled == true,
        ExecutionCondition = MapCondition(source.ExecutionCondition), HasExecutionCondition = source.ExecutionCondition?.IsEnabled == true,
        Configuration = MapTaskConfiguration(source.Configuration, taskId), RetryPolicy = MapRetryPolicy(source.RetryPolicy), TimeoutPolicy = MapTimeoutPolicy(source.TimeoutPolicy),
        DispatchType = source.DispatchType, OnErrorPolicy = source.OnErrorPolicy,
    };
}
