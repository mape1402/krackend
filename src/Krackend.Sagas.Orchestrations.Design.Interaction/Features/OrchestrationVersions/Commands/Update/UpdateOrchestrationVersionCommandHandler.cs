using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles update orchestration version command requests.
/// </summary>
public sealed class UpdateOrchestrationVersionCommandHandler : IRequestHandler<UpdateOrchestrationVersionCommand, bool>
{
    private readonly IOrchestrationVersionRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOrchestrationVersionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public UpdateOrchestrationVersionCommandHandler(IOrchestrationVersionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(UpdateOrchestrationVersionCommand request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.Design.Core.OrchestrationVersion current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);

        current.VersionLabel = request.VersionLabel;
        current.Description = request.Description;
        current.Checksum = new Checksum(request.Checksum);
        current.Notes = request.Notes;
        current.UpdatedOnUtc = DateTime.UtcNow;
        current.UpdatedBy = request.UpdatedBy;

        await _repository.Update(current, cancellationToken);
        return true;
    }
}


