using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Provides interaction operations for task.
/// </summary>
public sealed class TaskApplicationService : ITaskApplicationService
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskApplicationService"/> class.
    /// </summary>
    /// <param name="mediator">Mediator dependency.</param>
    public TaskApplicationService(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Creates a new resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Identifier or textual result of the operation.</returns>
    public Task<string> Create(CreateTaskDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <summary>
    /// Updates an existing resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public Task<bool> Update(UpdateTaskDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <summary>
    /// Deletes a resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public Task<bool> Delete(DeleteTaskDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <summary>
    /// Enables one task.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public Task<bool> Enable(EnableTaskDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <summary>
    /// Disables one task.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public Task<bool> Disable(DisableTaskDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <summary>
    /// Updates only execution condition of one task.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public Task<bool> SetExecutionCondition(SetTaskExecutionConditionCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <summary>
    /// Updates only transformation of one task.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public Task<bool> SetTransformation(SetTaskTransformationCommand command, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(command, cancellationToken);
    }

    /// <summary>
    /// Gets resources that match the query.
    /// </summary>
    /// <param name="query">Query to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Collection result.</returns>
    public Task<IEnumerable<TaskDefinitionModel>> GetAll(GetTaskDefinitionsQuery query, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(query, cancellationToken);
    }

    /// <summary>
    /// Gets one resource by identifier.
    /// </summary>
    /// <param name="query">Query to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Asynchronous operation result.</returns>
    public Task<TaskDefinitionModel> GetById(GetTaskDefinitionByIdQuery query, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(query, cancellationToken);
    }
}


