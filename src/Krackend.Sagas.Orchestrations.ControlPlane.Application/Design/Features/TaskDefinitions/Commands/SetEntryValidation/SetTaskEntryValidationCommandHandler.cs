using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles task entry validation updates.
/// </summary>
public sealed class SetTaskEntryValidationCommandHandler : IRequestHandler<SetTaskEntryValidationCommand, bool>
{
    private readonly ITaskRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetTaskEntryValidationCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Task repository.</param>
    public SetTaskEntryValidationCommandHandler(ITaskRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(SetTaskEntryValidationCommand request, CancellationToken cancellationToken)
    {
        await _repository.SetEntryValidation(
            PrimitiveParser.ParseId(request.Id),
            request.EntryValidation,
            cancellationToken);

        return true;
    }
}
