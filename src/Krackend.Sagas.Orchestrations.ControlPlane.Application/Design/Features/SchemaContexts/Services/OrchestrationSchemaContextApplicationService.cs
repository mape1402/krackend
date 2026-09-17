namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

/// <summary>
/// Loads orchestration versions and builds task schema contexts.
/// </summary>
public sealed class OrchestrationSchemaContextApplicationService : IOrchestrationSchemaContextApplicationService
{
    private readonly IOrchestrationVersionRepository _versionRepository;
    private readonly IOrchestrationSchemaContextBuilder _contextBuilder;
    private readonly IOrchestrationSchemaBindingSnapshotResolver _schemaBindingSnapshotResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationSchemaContextApplicationService"/> class.
    /// </summary>
    /// <param name="versionRepository">Orchestration version repository.</param>
    /// <param name="contextBuilder">Schema context builder.</param>
    public OrchestrationSchemaContextApplicationService(
        IOrchestrationVersionRepository versionRepository,
        IOrchestrationSchemaContextBuilder contextBuilder,
        IOrchestrationSchemaBindingSnapshotResolver schemaBindingSnapshotResolver = null)
    {
        _versionRepository = versionRepository ?? throw new ArgumentNullException(nameof(versionRepository));
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _schemaBindingSnapshotResolver = schemaBindingSnapshotResolver;
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

        if (_schemaBindingSnapshotResolver is not null)
        {
            await _schemaBindingSnapshotResolver.ResolveAsync(version, cancellationToken);
        }

        return await _contextBuilder.BuildForTask(
            version,
            PrimitiveParser.ParseId(query.TaskDefinitionId),
            cancellationToken);
    }
}
