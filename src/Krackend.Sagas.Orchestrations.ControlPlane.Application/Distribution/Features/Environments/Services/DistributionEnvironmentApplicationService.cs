using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Provides application operations for Control Plane distribution environments.
/// </summary>
public sealed class DistributionEnvironmentApplicationService : IDistributionEnvironmentApplicationService
{
    private readonly IDistributionEnvironmentRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributionEnvironmentApplicationService"/> class.
    /// </summary>
    /// <param name="repository">Distribution environment repository.</param>
    public DistributionEnvironmentApplicationService(IDistributionEnvironmentRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<string> Upsert(UpsertDistributionEnvironmentInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var parsedUlid = Ulid.NewUlid();
        var isUpdate = !string.IsNullOrWhiteSpace(input.EnvironmentId) && Ulid.TryParse(input.EnvironmentId, out parsedUlid);
        var existing = isUpdate
            ? await _repository.GetById(new Id(parsedUlid), cancellationToken)
            : null;

        var environment = new DistributionEnvironment
        {
            Id = isUpdate ? new Id(parsedUlid) : Id.New(),
            Name = input.Name.Trim(),
            Code = input.Code.Trim(),
            Description = input.Description?.Trim() ?? string.Empty,
            IsEnabled = input.IsEnabled,
            CreatedAtUtc = existing?.CreatedAtUtc ?? DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        if (isUpdate) await _repository.Update(environment, cancellationToken);
        else await _repository.Create(environment, cancellationToken);

        return environment.Id.ToString();
    }

    /// <inheritdoc />
    public async Task<ApplicationPagedResult<DistributionEnvironmentModel>> GetAll(ApplicationPagedSettings settings, CancellationToken cancellationToken = default)
    {
        var result = await _repository.GetAll(new PagedSettings(settings.PageNumber, settings.PageSize, Array.Empty<QueryFilter>(), Array.Empty<QuerySort>()), cancellationToken);
        return new ApplicationPagedResult<DistributionEnvironmentModel>
        {
            PageNumber = result.PageNumber,
            PageSize = result.PageSize,
            TotalRows = (int)result.TotalRows,
            TotalPages = result.TotalPages,
            Rows = result.Rows.Select(x => new DistributionEnvironmentModel
            {
                Id = x.Id.ToString(),
                Name = x.Name,
                Code = x.Code,
                Description = x.Description,
                IsEnabled = x.IsEnabled,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            }).ToArray()
        };
    }
}
