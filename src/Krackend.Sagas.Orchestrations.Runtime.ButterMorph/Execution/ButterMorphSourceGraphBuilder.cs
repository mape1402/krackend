namespace Krackend.Sagas.Orchestrations.Runtime.ButterMorph;

using System.Text.Json.Nodes;
using global::ButterMorph.Abstractions;
using global::ButterMorph.Core;
using global::ButterMorph.Json;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;

/// <summary>
/// Default source graph builder that exposes full context, trigger payload, and accumulated task payloads.
/// </summary>
public sealed class ButterMorphSourceGraphBuilder : IButterMorphSourceGraphBuilder
{
    private const string ContextAlias = "context";
    private const string TriggerAlias = "trigger";
    private const string RequestsAlias = "requests";
    private const string ResponsesAlias = "responses";
    private const string StagesPropertyName = "stages";
    private const string TasksPropertyName = "tasks";
    private const string RequestPropertyName = "request";
    private const string ResponsePropertyName = "response";

    private readonly IButterMorphAliasNameFormatter _aliasNameFormatter;
    private readonly JsonReader _jsonReader = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ButterMorphSourceGraphBuilder"/> class.
    /// </summary>
    /// <param name="aliasNameFormatter">Formats orchestration keys into ButterMorph-friendly names.</param>
    public ButterMorphSourceGraphBuilder(IButterMorphAliasNameFormatter aliasNameFormatter)
    {
        _aliasNameFormatter = aliasNameFormatter ?? throw new ArgumentNullException(nameof(aliasNameFormatter));
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IStructureGraph> Build(OrchestrationPayloadContext payloadContext)
    {
        ArgumentNullException.ThrowIfNull(payloadContext);

        var sources = new Dictionary<string, IStructureGraph>(StringComparer.OrdinalIgnoreCase);
        AddSource(sources, ContextAlias, payloadContext.ContextPayload);
        AddSource(sources, TriggerAlias, payloadContext.TriggerPayload);
        AddSource(sources, RequestsAlias, BuildTaskPayloadCollection(payloadContext.ContextPayload, RequestPropertyName));
        AddSource(sources, ResponsesAlias, BuildTaskPayloadCollection(payloadContext.ContextPayload, ResponsePropertyName));

        return sources;
    }

    private JsonNode BuildTaskPayloadCollection(JsonNode contextPayload, string payloadPropertyName)
    {
        var result = new JsonObject();
        if (contextPayload is not JsonObject root ||
            root[StagesPropertyName] is not JsonObject stages)
        {
            return result;
        }

        foreach (var stageEntry in stages)
        {
            if (stageEntry.Value is not JsonObject stage ||
                stage[TasksPropertyName] is not JsonObject tasks)
            {
                continue;
            }

            var stagePayloads = new JsonObject();
            foreach (var taskEntry in tasks)
            {
                if (taskEntry.Value is not JsonObject task ||
                    task[payloadPropertyName] is not JsonNode payload)
                {
                    continue;
                }

                stagePayloads[_aliasNameFormatter.Format(taskEntry.Key)] = payload.DeepClone();
            }

            if (stagePayloads.Count > 0)
            {
                result[_aliasNameFormatter.Format(stageEntry.Key)] = stagePayloads;
            }
        }

        return result;
    }

    private void AddSource(
        IDictionary<string, IStructureGraph> sources,
        string alias,
        JsonNode payload)
    {
        if (payload is null)
        {
            return;
        }

        var graph = _jsonReader.Read(new StructureInput
        {
            Format = "json",
            Content = payload.ToJsonString()
        });

        sources[alias] = graph;
    }
}
