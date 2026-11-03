namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using global::ButterMorph.Web.Razor;
using System.Security.Cryptography;
using System.Text;

/// <summary>Versions a designer session by its persisted source and target schemas.</summary>
public static class OrchestrationButterMorphContextVersion
{
    public static async Task<string> ResolveAsync(
        string contextKey,
        IOrchestrationButterMorphDesignerContextParser parser,
        IOrchestrationSchemaContextApplicationService schemas,
        IButterMorphDesignerHost mappingHost,
        IButterMorphValidationDesignerHost validationHost,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contextKey)) return null;
        OrchestrationSchemaContext schema;
        var isMapping = contextKey.Contains("-transform:", StringComparison.Ordinal);
        if (parser.TryParseTaskTransformation(contextKey, out var task) ||
            parser.TryParseTaskExecutionCondition(contextKey, out task))
            schema = await schemas.GetForTask(new GetTaskSchemaContextQuery(task.OrchestrationVersionId, task.TaskDefinitionId), cancellationToken);
        else if (parser.TryParseTaskCompensationTransformation(contextKey, out task) ||
                 parser.TryParseTaskCompensationExecutionCondition(contextKey, out task))
            schema = await schemas.GetForTaskCompensation(new GetTaskCompensationSchemaContextQuery(task.OrchestrationVersionId, task.TaskDefinitionId), cancellationToken);
        else if (parser.TryParseTriggerCompensationTransformation(contextKey, out var trigger) ||
                 parser.TryParseTriggerCompensationExecutionCondition(contextKey, out trigger))
            schema = await schemas.GetForTriggerCompensation(new GetTriggerCompensationSchemaContextQuery(trigger.OrchestrationVersionId, trigger.TriggerBindingId), cancellationToken);
        else if (parser.TryParseStageExecutionCondition(contextKey, out var stage))
            schema = await schemas.GetForStage(new GetStageSchemaContextQuery(stage.OrchestrationVersionId, stage.StageDefinitionId), cancellationToken);
        else
            return null;

        if (schema?.Signature is not { Length: 64 } signature || !signature.All(Uri.IsHexDigit))
            throw new InvalidOperationException("The designer schema signature is invalid.");
        var dsl = isMapping
            ? (await mappingHost.Load(new ButterMorphDesignerLoadRequest { ContextKey = contextKey })).InitialDslContent
            : (await validationHost.Load(new ButterMorphValidationDesignerLoadRequest { ContextKey = contextKey })).InitialDslContent;
        var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature + "\n" + (dsl ?? string.Empty)))).ToLowerInvariant();
        return string.Join(':', contextKey.Split(':').Take(3)) + ":" + version;
    }
}
