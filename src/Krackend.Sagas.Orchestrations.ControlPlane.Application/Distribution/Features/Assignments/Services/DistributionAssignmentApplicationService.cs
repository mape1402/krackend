using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public sealed class ReleaseTargetApplicationService : IReleaseTargetApplicationService
{
    private readonly IReleaseTargetRepository _repository;

    public ReleaseTargetApplicationService(IReleaseTargetRepository repository) { _repository = repository; }

    public async Task<ApplicationPagedResult<ReleaseTargetModel>> GetAll(ApplicationPagedSettings settings, CancellationToken cancellationToken = default)
    {
        var result = await _repository.GetAll(new PagedSettings(settings.PageNumber, settings.PageSize, Array.Empty<QueryFilter>(), Array.Empty<QuerySort>()), cancellationToken);
        return new ApplicationPagedResult<ReleaseTargetModel>
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

    public async Task<IReadOnlyCollection<ReleaseTargetModel>> GetByRelease(string releaseId, CancellationToken cancellationToken = default)
    {
        var rows = await _repository.GetByRelease(new Id(Ulid.Parse(releaseId)), cancellationToken);
        return rows.Select(MapTarget).ToArray();
    }

    public async Task<IReadOnlyCollection<ReleaseTargetModel>> GetByReleases(IReadOnlyCollection<string> releaseIds, CancellationToken cancellationToken = default)
        => (await _repository.GetByReleases(releaseIds.Select(x => new Id(Ulid.Parse(x))).Distinct().ToArray(), cancellationToken)).Select(MapTarget).ToArray();

    private static ReleaseTargetModel MapTarget(Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core.ReleaseTarget x) => new()
    {
        Id = x.Id.ToString(), RuntimeNodeId = x.RuntimeNodeId.ToString(), ArtifactId = x.ArtifactId.ToString(),
        ReleaseId = x.ReleaseId?.ToString() ?? string.Empty, RolloutGroup = x.RolloutGroup,
        Status = x.Status.ToString(), ActivationStatus = x.ActivationStatus.ToString(), AssignedAtUtc = x.AssignedAtUtc
    };
}

