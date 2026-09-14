using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles update task definition command requests.
/// </summary>
public sealed class UpdateTaskDefinitionCommandHandler : IRequestHandler<UpdateTaskDefinitionCommand, bool>
{
    private readonly ITaskRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTaskDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public UpdateTaskDefinitionCommandHandler(ITaskRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(UpdateTaskDefinitionCommand request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TaskDefinition current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);

        current.Key = request.Key;
        current.Name = request.Name;
        current.Order = request.Order;
        current.Notes = request.Notes;
        current.Kind = request.Kind;
        current.ExecutionMode = request.ExecutionMode;
        current.ParallelGroupId = string.IsNullOrWhiteSpace(request.ParallelGroupId)
            ? null
            : PrimitiveParser.ParseId(request.ParallelGroupId);
        current.ExecutionCondition = request.ExecutionCondition;
        current.HasExecutionCondition = request.ExecutionCondition is not null;
        current.Transformation = request.Transformation;
        current.HasTransformation = request.Transformation is not null;
        current.Configuration = request.Configuration;
        current.RetryPolicy = request.RetryPolicy;
        current.TimeoutPolicy = request.TimeoutPolicy;
        current.OnErrorPolicy = request.OnErrorPolicy;
        current.CompensationDefinition = request.CompensationDefinition;
        current.DispatchType = request.DispatchType;
        current.IsEnabled = request.IsEnabled;

        await _repository.Update(current, cancellationToken);
        return true;
    }
}

