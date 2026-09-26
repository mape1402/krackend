namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

/// <summary>
/// Loads orchestration versions and builds task schema contexts.
/// </summary>
public sealed class OrchestrationSchemaContextApplicationService : IOrchestrationSchemaContextApplicationService
{
    private readonly IOrchestrationVersionRepository _versionRepository;
    private readonly IStageRepository _stageRepository;
    private readonly ITriggerBindingRepository _triggerBindingRepository;
    private readonly IOrchestrationSchemaContextBuilder _contextBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationSchemaContextApplicationService"/> class.
    /// </summary>
    /// <param name="versionRepository">Orchestration version repository.</param>
    /// <param name="contextBuilder">Schema context builder.</param>
    /// <param name="schemaBindingSnapshotResolver">Legacy parameter retained for callers that still pass the resolver. Schema context reads use only stored snapshots.</param>
    /// <param name="stageRepository">Stage repository used to load the complete orchestration graph.</param>
    /// <param name="triggerBindingRepository">Trigger binding repository used to load trigger schemas.</param>
    public OrchestrationSchemaContextApplicationService(
        IOrchestrationVersionRepository versionRepository,
        IOrchestrationSchemaContextBuilder contextBuilder,
        IOrchestrationSchemaBindingSnapshotResolver schemaBindingSnapshotResolver = null,
        IStageRepository stageRepository = null,
        ITriggerBindingRepository triggerBindingRepository = null)
    {
        _versionRepository = versionRepository ?? throw new ArgumentNullException(nameof(versionRepository));
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _ = schemaBindingSnapshotResolver;
        _stageRepository = stageRepository;
        _triggerBindingRepository = triggerBindingRepository;
    }

    /// <inheritdoc />
    public async Task<OrchestrationSchemaContext> GetForTask(
        GetTaskSchemaContextQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var version = await _versionRepository.GetById(
            PrimitiveParser.ParseId(query.OrchestrationVersionId),
            cancellationToken);
        version = await LoadCompleteVersionGraph(version, cancellationToken);

        return await _contextBuilder.BuildForTask(
            version,
            PrimitiveParser.ParseId(query.TaskDefinitionId),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrchestrationSchemaContext> GetForStage(
        GetStageSchemaContextQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var version = await _versionRepository.GetById(
            PrimitiveParser.ParseId(query.OrchestrationVersionId),
            cancellationToken);
        version = await LoadCompleteVersionGraph(version, cancellationToken);

        return await _contextBuilder.BuildForStage(
            version,
            PrimitiveParser.ParseId(query.StageDefinitionId),
            cancellationToken);
    }

    private async Task<OrchestrationVersion> LoadCompleteVersionGraph(
        OrchestrationVersion version,
        CancellationToken cancellationToken)
    {
        if (_triggerBindingRepository is not null)
        {
            version.TriggerBindings = (await _triggerBindingRepository.GetAll(version.Id, cancellationToken)).ToList();
        }

        if (_stageRepository is not null)
        {
            var stageShells = await _stageRepository.GetAll(version.Id, cancellationToken);
            var stages = new List<StageDefinition>();
            foreach (var stage in stageShells.OrderBy(x => x.Order).ThenBy(x => x.Key, StringComparer.Ordinal))
            {
                stages.Add(await _stageRepository.GetById(stage.Id, cancellationToken));
            }

            version.StageDefinitions = stages;
        }

        return version;
    }
}
