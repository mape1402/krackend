using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles stage entry validation updates.
/// </summary>
public sealed class SetStageEntryValidationCommandHandler : IRequestHandler<SetStageEntryValidationCommand, bool>
{
    private readonly IStageRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetStageEntryValidationCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Stage repository.</param>
    public SetStageEntryValidationCommandHandler(IStageRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(SetStageEntryValidationCommand request, CancellationToken cancellationToken)
    {
        await _repository.SetEntryValidation(
            PrimitiveParser.ParseId(request.Id),
            request.EntryValidation,
            cancellationToken);

        return true;
    }
}
