using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Krackend.Sagas.Orchestrations.Distribution.Core;
using Krackend.Sagas.Orchestrations.Distribution.Enums;
using Krackend.Sagas.Orchestrations.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public sealed class RuntimeNodeInteractionService : IRuntimeNodeInteractionService
{
    private readonly IRuntimeNodeRepository _repository;
    private readonly IEnvironmentRepository _environmentRepository;

    public RuntimeNodeInteractionService(
        IRuntimeNodeRepository repository,
        IEnvironmentRepository environmentRepository)
    {
        _repository = repository;
        _environmentRepository = environmentRepository;
    }

    public async Task<string> Upsert(UpsertRuntimeNodeInput input, CancellationToken cancellationToken = default)
    {
        var parsedUlid = Ulid.NewUlid();
        var isUpdate = !string.IsNullOrWhiteSpace(input.RuntimeNodeId) && Ulid.TryParse(input.RuntimeNodeId, out parsedUlid);
        RuntimeNode existing = null;
        if (isUpdate)
        {
            existing = await _repository.GetById(new Id(parsedUlid), cancellationToken);
        }
        if (string.IsNullOrWhiteSpace(input.EnvironmentId))
        {
            throw new InvalidOperationException("Environment is required.");
        }
        var environmentId = new Id(Ulid.Parse(input.EnvironmentId));
        var environment = await _environmentRepository.GetById(environmentId, cancellationToken);
        if (!environment.IsEnabled)
        {
            throw new InvalidOperationException("Selected environment is disabled.");
        }

        var entity = new RuntimeNode
        {
            Id = isUpdate ? new Id(parsedUlid) : Id.New(),
            Name = input.Name.Trim(),
            Code = input.Code.Trim(),
            EnvironmentId = environmentId,
            EnvironmentName = environment.Name,
            DistributionMode = input.DistributionMode,
            EndpointBaseUri = input.EndpointBaseUri?.Trim() ?? string.Empty,
            EndpointApiPath = existing?.EndpointApiPath ?? string.Empty,
            AuthenticationMode = existing?.AuthenticationMode ?? RuntimeAuthenticationMode.None,
            ClientId = existing?.ClientId ?? string.Empty,
            SecretReference = existing?.SecretReference ?? string.Empty,
            ApiKeyReference = existing?.ApiKeyReference ?? string.Empty,
            Status = existing?.Status ?? RuntimeNodeStatus.Active,
            IsEnabled = existing?.IsEnabled ?? true,
            Description = input.Description?.Trim() ?? string.Empty,
            RegisteredAtUtc = existing?.RegisteredAtUtc ?? DateTime.UtcNow,
            LastUpdatedAtUtc = DateTime.UtcNow
        };

        if (isUpdate) await _repository.Update(entity, cancellationToken);
        else await _repository.Create(entity, cancellationToken);

        return entity.Id.ToString();
    }

    public Task SetEnabled(string runtimeNodeId, bool isEnabled, CancellationToken cancellationToken = default)
        => _repository.SetIsEnabled(new Id(Ulid.Parse(runtimeNodeId)), isEnabled, cancellationToken);

    public async Task<InteractionPagedResult<RuntimeNodeModel>> GetAll(InteractionPagedSettings settings, CancellationToken cancellationToken = default)
    {
        var result = await _repository.GetAll(new PagedSettings(settings.PageNumber, settings.PageSize, Array.Empty<QueryFilter>(), Array.Empty<QuerySort>()), cancellationToken);
        return new InteractionPagedResult<RuntimeNodeModel>
        {
            PageNumber = result.PageNumber,
            PageSize = result.PageSize,
            TotalRows = (int)result.TotalRows,
            TotalPages = result.TotalPages,
            Rows = result.Rows.Select(x => new RuntimeNodeModel
            {
                Id = x.Id.ToString(), Name = x.Name, Code = x.Code, EnvironmentId = x.EnvironmentId.ToString(), EnvironmentName = x.EnvironmentName,
                DistributionMode = x.DistributionMode.ToString(), EndpointBaseUri = x.EndpointBaseUri,
                Status = x.Status.ToString(), IsEnabled = x.IsEnabled, Description = x.Description, RegisteredAtUtc = x.RegisteredAtUtc
            }).ToArray()
        };
    }
}

