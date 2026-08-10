using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Krackend.Sagas.Orchestrations.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public sealed class ReleaseTargetInteractionService : IReleaseTargetInteractionService
{
    private readonly IReleaseTargetRepository _repository;

    public ReleaseTargetInteractionService(IReleaseTargetRepository repository) { _repository = repository; }

    public async Task<InteractionPagedResult<ReleaseTargetModel>> GetAll(InteractionPagedSettings settings, CancellationToken cancellationToken = default)
    {
        var result = await _repository.GetAll(new PagedSettings(settings.PageNumber, settings.PageSize, Array.Empty<QueryFilter>(), Array.Empty<QuerySort>()), cancellationToken);
        return new InteractionPagedResult<ReleaseTargetModel>
        {
            PageNumber = result.PageNumber,
            PageSize = result.PageSize,
            TotalRows = (int)result.TotalRows,
            TotalPages = result.TotalPages,
            Rows = result.Rows.Select(x => new ReleaseTargetModel
            {
                Id = x.Id.ToString(), RuntimeNodeId = x.RuntimeNodeId.ToString(), ArtifactId = x.ArtifactId.ToString(),
                ReleaseId = x.ReleaseId?.ToString() ?? string.Empty, RolloutGroup = x.RolloutGroup,
                Status = x.Status.ToString(), ActivationStatus = x.ActivationStatus.ToString(), AssignedAtUtc = x.AssignedAtUtc
            }).ToArray()
        };
    }

    public async Task<IReadOnlyCollection<ReleaseAttemptModel>> GetAttempts(string assignmentId, CancellationToken cancellationToken = default)
    {
        var attempts = await _repository.GetAttempts(new Id(Ulid.Parse(assignmentId)), cancellationToken);
        return attempts.Select(x => new ReleaseAttemptModel
        {
            Id = x.Id.ToString(), ReleaseTargetId = x.ReleaseTargetId.ToString(), Action = x.Action,
            InitiatedBy = x.InitiatedBy, StartedAtUtc = x.StartedAtUtc, FinishedAtUtc = x.FinishedAtUtc,
            Succeeded = x.Succeeded, ErrorCode = x.ErrorCode, ErrorMessage = x.ErrorMessage, ExternalReference = x.ExternalReference
        }).ToArray();
    }
}

