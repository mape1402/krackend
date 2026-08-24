using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public sealed class RuntimeEnvironmentApplicationService : IRuntimeEnvironmentApplicationService
{
    private readonly IEnvironmentRepository _repository;

    public RuntimeEnvironmentApplicationService(IEnvironmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<string> Upsert(UpsertRuntimeEnvironmentInput input, CancellationToken cancellationToken = default)
    {
        var parsedUlid = Ulid.NewUlid();
        var isUpdate = !string.IsNullOrWhiteSpace(input.EnvironmentId) && Ulid.TryParse(input.EnvironmentId, out parsedUlid);
        RuntimeEnvironment existing = null;
        if (isUpdate)
        {
            existing = await _repository.GetById(new Id(parsedUlid), cancellationToken);
        }

        var entity = new RuntimeEnvironment
        {
            Id = isUpdate ? new Id(parsedUlid) : Id.New(),
            Name = input.Name.Trim(),
            Code = input.Code.Trim(),
            Description = input.Description?.Trim() ?? string.Empty,
            IsEnabled = existing?.IsEnabled ?? true,
            CreatedAtUtc = existing?.CreatedAtUtc ?? DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        if (isUpdate) await _repository.Update(entity, cancellationToken);
        else await _repository.Create(entity, cancellationToken);

        return entity.Id.ToString();
    }

    public Task SetEnabled(string environmentId, bool isEnabled, CancellationToken cancellationToken = default)
        => _repository.SetIsEnabled(new Id(Ulid.Parse(environmentId)), isEnabled, cancellationToken);

    public async Task<ApplicationPagedResult<RuntimeEnvironmentModel>> GetAll(ApplicationPagedSettings settings, CancellationToken cancellationToken = default)
    {
        var result = await _repository.GetAll(new PagedSettings(settings.PageNumber, settings.PageSize, Array.Empty<QueryFilter>(), Array.Empty<QuerySort>()), cancellationToken);
        return new ApplicationPagedResult<RuntimeEnvironmentModel>
        {
            PageNumber = result.PageNumber,
            PageSize = result.PageSize,
            TotalRows = (int)result.TotalRows,
            TotalPages = result.TotalPages,
            Rows = result.Rows.Select(x => new RuntimeEnvironmentModel
            {
                Id = x.Id.ToString(),
                Name = x.Name,
                Code = x.Code,
                Description = x.Description,
                IsEnabled = x.IsEnabled,
                CreatedAtUtc = x.CreatedAtUtc
            }).ToArray()
        };
    }
}

