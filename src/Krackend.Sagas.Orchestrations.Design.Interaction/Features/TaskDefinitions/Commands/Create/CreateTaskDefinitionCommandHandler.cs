using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles create task definition command requests.
/// </summary>
public sealed class CreateTaskDefinitionCommandHandler : IRequestHandler<CreateTaskDefinitionCommand, string>
{
    private readonly ITaskRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTaskDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public CreateTaskDefinitionCommandHandler(ITaskRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
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
            Transformation = request.Transformation,
            Configuration = request.Configuration,
            RetryPolicy = request.RetryPolicy,
            TimeoutPolicy = request.TimeoutPolicy,
            OnErrorPolicy = request.OnErrorPolicy,
            CompensationDefinition = request.CompensationDefinition,
            DispatchType = request.DispatchType,
            IsEnabled = request.IsEnabled,
        };

        await _repository.Create(model, cancellationToken);
        return id.ToString();
    }
}

