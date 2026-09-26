using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles create task definition command requests.
/// </summary>
public sealed class CreateTaskDefinitionCommandHandler : IRequestHandler<CreateTaskDefinitionCommand, string>
{
    private readonly ITaskRepository _repository;
    private readonly IOrchestrationSchemaBindingSnapshotResolver _schemaBindingSnapshotResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTaskDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    /// <param name="schemaBindingSnapshotResolver">Schema binding snapshot resolver.</param>
    public CreateTaskDefinitionCommandHandler(
        ITaskRepository repository,
        IOrchestrationSchemaBindingSnapshotResolver schemaBindingSnapshotResolver = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _schemaBindingSnapshotResolver = schemaBindingSnapshotResolver;
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Identifier or textual result of the operation.</returns>
    public async Task<string> Handle(CreateTaskDefinitionCommand request, CancellationToken cancellationToken)
    {
        Id id = Id.New();

        TaskDefinition model = new()
        {
            Id = id,
            StageDefinitionId = PrimitiveParser.ParseId(request.StageDefinitionId),
            Key = request.Key,
            Name = request.Name,
            Order = request.Order,
            Notes = request.Notes,
            Kind = request.Kind,
            ExecutionMode = request.ExecutionMode,
            ParallelGroupId = string.IsNullOrWhiteSpace(request.ParallelGroupId)
                ? null
                : PrimitiveParser.ParseId(request.ParallelGroupId),
            ExecutionCondition = request.ExecutionCondition,
            HasExecutionCondition = request.ExecutionCondition is not null,
            Transformation = request.Transformation,
            HasTransformation = request.Transformation is not null,
            Configuration = request.Configuration,
            RetryPolicy = request.RetryPolicy,
            TimeoutPolicy = request.TimeoutPolicy,
            OnErrorPolicy = request.OnErrorPolicy,
            CompensationDefinition = request.CompensationDefinition,
            DispatchType = request.DispatchType,
            IsEnabled = request.IsEnabled,
        };

        if (_schemaBindingSnapshotResolver is not null)
        {
            await _schemaBindingSnapshotResolver.ResolveTaskAsync(model, cancellationToken);
            await _schemaBindingSnapshotResolver.ResolveCompensationAsync(model.CompensationDefinition, cancellationToken);
        }

        await _repository.Create(model, cancellationToken);
        return id.ToString();
    }
}

