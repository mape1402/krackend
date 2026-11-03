namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FluentValidation;
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

/// <summary>Copies portable roadmap elements between versions.</summary>
public sealed class RoadmapElementPackageService(
    IStageRepository stages, ITaskRepository tasks,
    ITriggerBindingRepository triggers, IOrchestrationVersionEditGuard guard, IControlPlaneUnitOfWork unitOfWork)
{
    private const string PackageKind = "krackend-roadmap-element";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    /// <summary>Builds a portable JSON package from persisted data.</summary>
    public async Task<string> CopyAsync(string type, string sourceId, string sourceVersionId, CancellationToken token = default)
    {
        var versionId = ParseId(sourceVersionId);
        var id = ParseId(sourceId);
        object element;
        switch (type)
        {
            case "task":
                var task = await tasks.GetById(id, token);
                var parent = await stages.GetById(task.StageDefinitionId, token);
                if (parent.OrchestrationVersionId != versionId) throw new InvalidOperationException("Task does not belong to this roadmap.");
                element = PortableTask(task);
                break;
            case "stage":
                var stage = await stages.GetById(id, token);
                if (stage.OrchestrationVersionId != versionId) throw new InvalidOperationException("Stage does not belong to this roadmap.");
                var stageCondition = PortableCondition(stage.ExecutionCondition);
                var portableStage = new StageDefinition
                {
                    Key = stage.Key, Name = stage.Name, Description = stage.Description, Order = stage.Order,
                    IsEnabled = stage.IsEnabled, ExecutionCondition = stageCondition,
                    HasExecutionCondition = stageCondition is not null && stage.HasExecutionCondition,
                    TaskDefinitions = (await tasks.GetAll(id, token)).OrderBy(x => x.Order).Select(PortableTask).ToList()
                };
                element = portableStage;
                break;
            case "trigger":
                var trigger = await triggers.GetById(id, token);
                if (trigger.OrchestrationVersionId != versionId) throw new InvalidOperationException("Trigger does not belong to this roadmap.");
                var portableTrigger = Copy(new TriggerBinding
                {
                    Key = trigger.Key, TriggerType = trigger.TriggerType, TriggerChannel = trigger.TriggerChannel,
                    IsEnabled = trigger.IsEnabled, Description = trigger.Description,
                    CompensationDefinition = PortableCompensation(trigger.CompensationDefinition)
                });
                ResetTriggerBindings(portableTrigger, default);
                MakePortable(portableTrigger.CompensationDefinition);
                element = portableTrigger;
                break;
            default: throw new InvalidOperationException("Unknown roadmap element type.");
        }
        return JsonSerializer.Serialize(new Envelope(PackageKind, 1, type, JsonSerializer.SerializeToElement(element, element.GetType(), JsonOptions)), JsonOptions);
    }

    /// <summary>Validates and creates an element in a draft roadmap.</summary>
    public async Task<string> PasteAsync(string json, string type, string destinationVersionId, string destinationStageId = null, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 2_000_000) throw new InvalidOperationException("The clipboard does not contain a valid roadmap element.");
        Envelope package;
        try { package = JsonSerializer.Deserialize<Envelope>(json, JsonOptions); }
        catch (JsonException) { throw new InvalidOperationException("The clipboard does not contain a valid roadmap element."); }
        if (package is null || package.Kind != PackageKind || package.SchemaVersion != 1 || package.ElementType != type)
            throw new InvalidOperationException("The copied element is not compatible with this section.");
        var versionId = ParseId(destinationVersionId);
        var result = string.Empty;
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await guard.EnsureVersionIsDraft(destinationVersionId, ct);
            switch (type)
            {
                case "task":
                    var targetStage = await stages.GetById(ParseId(destinationStageId), ct);
                    if (targetStage.OrchestrationVersionId != versionId) throw new InvalidOperationException("The stage does not belong to this roadmap.");
                    var newTask = Deserialize<TaskDefinition>(package.Data);
                    var existingTasks = (await tasks.GetAll(targetStage.Id, ct)).ToArray();
                    PrepareTask(newTask, targetStage.Id, existingTasks.Select(x => x.Key), existingTasks.Select(x => x.Order).DefaultIfEmpty(-1).Max() + 1);
                    ValidateTask(newTask);
                    await tasks.Create(newTask, ct);
                    result = newTask.Id.ToString();
                    break;
                case "stage":
                    var newStage = Deserialize<StageDefinition>(package.Data);
                    var existingStages = (await stages.GetAll(versionId, ct)).ToArray();
                    var stageTasks = newStage.TaskDefinitions ?? [];
                    newStage.Id = Id.New();
                    newStage.OrchestrationVersionId = versionId;
                    newStage.Key = UniqueKey(newStage.Key, existingStages.Select(x => x.Key));
                    newStage.Order = existingStages.Select(x => x.Order).DefaultIfEmpty(-1).Max() + 1;
                    newStage.ExecutionCondition = PortableCondition(newStage.ExecutionCondition);
                    newStage.HasExecutionCondition = newStage.ExecutionCondition is not null && newStage.HasExecutionCondition;
                    newStage.ParallelGroups = [];
                    newStage.BranchRules = [];
                    new CreateStageDefinitionCommandValidator().ValidateAndThrow(new CreateStageDefinitionCommand(versionId.ToString(), newStage.Key, newStage.Name, newStage.Description, newStage.Order, newStage.ExecutionCondition));
                    var keys = new List<string>();
                    var position = 0;
                    foreach (var child in stageTasks.OrderBy(x => x.Order))
                    {
                        PrepareTask(child, newStage.Id, keys, position++);
                        ValidateTask(child);
                        keys.Add(child.Key);
                    }
                    newStage.TaskDefinitions = [];
                    await stages.Create(newStage, ct);
                    foreach (var child in stageTasks) await tasks.Create(child, ct);
                    result = newStage.Id.ToString();
                    break;
                case "trigger":
                    var newTrigger = Deserialize<TriggerBinding>(package.Data);
                    var existingTriggers = (await triggers.GetAll(versionId, ct)).ToArray();
                    newTrigger.Id = Id.New();
                    newTrigger.OrchestrationVersionId = versionId;
                    newTrigger.Key = UniqueKey(newTrigger.Key, existingTriggers.Select(x => x.Key));
                    ResetTriggerBindings(newTrigger, newTrigger.Id);
                    MakePortable(newTrigger.CompensationDefinition);
                    ResetConfigBindings(newTrigger.CompensationDefinition?.Configuration, newTrigger.Id);
                    new CreateTriggerBindingCommandValidator().ValidateAndThrow(new CreateTriggerBindingCommand(versionId.ToString(), newTrigger.Key, newTrigger.TriggerType, newTrigger.TriggerChannel, newTrigger.IsEnabled, newTrigger.Description, newTrigger.CompensationDefinition));
                    await triggers.Create(newTrigger, ct);
                    result = newTrigger.Id.ToString();
                    break;
                default: throw new InvalidOperationException("Unknown roadmap element type.");
            }
        }, token);
        return result;
    }

    private static TaskDefinition PortableTask(TaskDefinition source)
    {
        var condition = PortableCondition(source.ExecutionCondition);
        var copy = Copy(new TaskDefinition
        {
            Key = source.Key, Name = source.Name, Order = source.Order, Notes = source.Notes, Kind = source.Kind,
            ExecutionMode = TaskExecutionMode.Sequential, ExecutionCondition = condition,
            HasExecutionCondition = condition is not null && source.HasExecutionCondition,
            Configuration = source.Configuration, RetryPolicy = source.RetryPolicy, TimeoutPolicy = source.TimeoutPolicy,
            OnErrorPolicy = source.OnErrorPolicy, CompensationDefinition = PortableCompensation(source.CompensationDefinition),
            DispatchType = source.DispatchType, IsEnabled = source.IsEnabled
        });
        ResetConfigBindings(copy.Configuration, default);
        ResetConfigBindings(copy.CompensationDefinition?.Configuration, default);
        return copy;
    }

    private static void PrepareTask(TaskDefinition task, Id stageId, IEnumerable<string> keys, int order)
    {
        task.Id = Id.New();
        task.StageDefinitionId = stageId;
        task.Key = UniqueKey(task.Key, keys);
        task.Order = order;
        task.ParallelGroupId = null;
        task.ExecutionMode = TaskExecutionMode.Sequential;
        task.ExecutionCondition = PortableCondition(task.ExecutionCondition);
        task.HasExecutionCondition = task.ExecutionCondition is not null && task.HasExecutionCondition;
        task.Transformation = null;
        task.HasTransformation = false;
        MakePortable(task.CompensationDefinition);
        ResetConfigBindings(task.Configuration, task.Id);
        ResetConfigBindings(task.CompensationDefinition?.Configuration, task.Id);
    }

    private static void MakePortable(CompensationDefinition compensation)
    {
        if (compensation is null) return;
        compensation.ExecutionCondition = PortableCondition(compensation.ExecutionCondition);
        compensation.HasExecutionCondition = compensation.ExecutionCondition is not null && compensation.HasExecutionCondition;
        compensation.Transformation = null;
        compensation.HasTransformation = false;
    }

    private static CompensationDefinition PortableCompensation(CompensationDefinition source)
    {
        if (source is null) return null;
        var condition = PortableCondition(source.ExecutionCondition);
        return new CompensationDefinition
        {
            CompensationTaskKind = source.CompensationTaskKind, Configuration = source.Configuration,
            RetryPolicy = source.RetryPolicy, TimeoutPolicy = source.TimeoutPolicy, DispatchType = source.DispatchType,
            OnErrorPolicy = source.OnErrorPolicy, ExecutionCondition = condition,
            HasExecutionCondition = condition is not null && source.HasExecutionCondition
        };
    }

    private static ExecutionCondition PortableCondition(ExecutionCondition condition)
    {
        var expression = (condition?.Configuration as DslConditionConfiguration)?.Expression.ToString()?.Trim();
        return string.Equals(expression, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(expression, "false", StringComparison.OrdinalIgnoreCase) ? condition : null;
    }

    private static void ResetTriggerBindings(TriggerBinding trigger, Id owner)
    {
        ResetBinding(trigger.TriggerChannel?.SchemaBinding, ElementType.Orchestration, owner);
        ResetConfigBindings(trigger.CompensationDefinition?.Configuration, owner);
    }
    private static void ResetConfigBindings(ITaskConfiguration configuration, Id owner)
    {
        if (configuration is MessagingTaskConfiguration messaging)
        {
            ResetBinding(messaging.SchemaBinding, ElementType.Task, owner);
            ResetBinding(messaging.RequestSchemaBinding, ElementType.Task, owner);
            ResetBinding(messaging.ResponseSchemaBinding, ElementType.Task, owner);
        }
        else if (configuration is HttpTaskConfiguration http) ResetBinding(http.SchemaBinding, ElementType.Task, owner);
    }
    private static void ResetBinding(SchemaBinding binding, ElementType type, Id owner)
    {
        if (binding is null) return;
        binding.Id = owner == default ? default : Id.New();
        binding.ElementId = owner;
        binding.ElementType = type;
    }

    private static void ValidateTask(TaskDefinition task) => new CreateTaskDefinitionCommandValidator().ValidateAndThrow(
        new CreateTaskDefinitionCommand(task.StageDefinitionId.ToString(), task.Key, task.Name, task.Order, task.Notes, task.Kind, task.ExecutionMode, null,
            task.ExecutionCondition, task.Transformation, task.Configuration, task.RetryPolicy, task.TimeoutPolicy, task.OnErrorPolicy, task.CompensationDefinition, task.DispatchType, task.IsEnabled));
    private static string UniqueKey(string key, IEnumerable<string> existing)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("The copied element has no key.");
        var keys = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!keys.Contains(key)) return key;
        for (var index = 1; ; index++)
        {
            var suffix = index == 1 ? "_copy" : $"_copy{index}";
            var candidate = key[..Math.Min(key.Length, 128 - suffix.Length)].TrimEnd('.', '_') + suffix;
            if (!keys.Contains(candidate)) return candidate;
        }
    }
    private static Id ParseId(string value) => Ulid.TryParse(value, out var id) ? new(id) : throw new InvalidOperationException("Invalid roadmap identifier.");
    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions);
    private static T Deserialize<T>(JsonElement value)
    {
        try { return value.Deserialize<T>(JsonOptions) ?? throw new InvalidOperationException("The copied element is empty."); }
        catch (JsonException) { throw new InvalidOperationException("The copied element is invalid."); }
    }
    private sealed record Envelope(string Kind, int SchemaVersion, string ElementType, JsonElement Data);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            Type[] types = info.Type == typeof(ITaskConfiguration) ? [typeof(MessagingTaskConfiguration), typeof(HttpTaskConfiguration), typeof(PluginTaskConfiguration), typeof(HumanApprovalTaskConfiguration)] :
                info.Type == typeof(ITriggerChannel) ? [typeof(EventTriggerChannel)] :
                info.Type == typeof(IConditionConfiguration) ? [typeof(DslConditionConfiguration)] :
                info.Type == typeof(ITransformationConfiguration) ? [typeof(DslTransformationConfiguration)] :
                info.Type == typeof(IValidationConfiguration) ? [typeof(DslValidationConfiguration)] :
                info.Type == typeof(IRetryStrategy) ? [typeof(FixedRetryStrategy)] :
                info.Type == typeof(ITimeoutBehaviorPolicy) ? [typeof(FailTimeoutBehaviorPolicy), typeof(WaitTimeoutBehaviorPolicy), typeof(ReconcileTimeoutBehaviorPolicy)] : [];
            if (types.Length == 0) return;
            info.PolymorphismOptions = new JsonPolymorphismOptions { TypeDiscriminatorPropertyName = "$type" };
            foreach (var type in types) info.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(type, type.Name));
        });
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = resolver, MaxDepth = 64 };
        options.Converters.Add(new IdConverter());
        return options;
    }
    private sealed class IdConverter : JsonConverter<Id>
    {
        public override Id Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ParseId(reader.GetString());
        public override void Write(Utf8JsonWriter writer, Id value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
    }
}

/// <summary>Clipboard data and destination for a roadmap paste operation.</summary>
public sealed record RoadmapElementPasteRequest(string Package, string ElementType, string VersionId, string StageId);
