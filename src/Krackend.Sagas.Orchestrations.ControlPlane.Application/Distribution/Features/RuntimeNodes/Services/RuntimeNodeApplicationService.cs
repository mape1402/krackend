using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public sealed class RuntimeNodeApplicationService : IRuntimeNodeApplicationService
{
    private readonly IRuntimeNodeRepository _repository;

    public RuntimeNodeApplicationService(IRuntimeNodeRepository repository)
    {
        _repository = repository;
    }

    public async Task<string> Upsert(UpsertRuntimeNodeInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var parsedUlid = Ulid.NewUlid();
        var isUpdate = !string.IsNullOrWhiteSpace(input.RuntimeNodeId) && Ulid.TryParse(input.RuntimeNodeId, out parsedUlid);
        RuntimeNode existing = null;
        if (isUpdate)
        {
            existing = await _repository.GetById(new Id(parsedUlid), cancellationToken);
            if (existing.IsDeleted)
            {
                throw new InvalidOperationException("Runtime node was deleted and cannot be updated.");
            }
        }

        var entity = new RuntimeNode
        {
            Id = isUpdate ? new Id(parsedUlid) : Id.New(),
            Name = input.Name.Trim(),
            Code = input.Code.Trim(),
            DistributionMode = input.DistributionMode,
            EndpointBaseUri = input.EndpointBaseUri?.Trim() ?? string.Empty,
            EndpointApiPath = existing?.EndpointApiPath ?? string.Empty,
            Status = existing?.Status ?? RuntimeNodeStatus.Pending,
            IsEnabled = existing?.Status == RuntimeNodeStatus.Enabled,
            IsDeleted = existing?.IsDeleted ?? false,
            DeletedAtUtc = existing?.DeletedAtUtc,
            Description = input.Description?.Trim() ?? string.Empty,
            AccessTokenTtlSeconds = existing?.AccessTokenTtlSeconds > 0 ? existing.AccessTokenTtlSeconds : 86_400,
            TokenRefreshSkewSeconds = existing?.TokenRefreshSkewSeconds > 0 ? existing.TokenRefreshSkewSeconds : 300,
            TokenValidationCacheTtlSeconds = existing?.TokenValidationCacheTtlSeconds > 0 ? existing.TokenValidationCacheTtlSeconds : 300,
            InboundClientId = existing?.InboundClientId ?? string.Empty,
            InboundKeyId = existing?.InboundKeyId ?? string.Empty,
            InboundSecretHash = existing?.InboundSecretHash ?? string.Empty,
            InboundAllowedScopes = existing?.InboundAllowedScopes ?? string.Empty,
            InboundCredentialStatus = existing?.InboundCredentialStatus ?? ConnectionCredentialStatus.Missing,
            InboundCredentialCreatedAtUtc = existing?.InboundCredentialCreatedAtUtc,
            InboundCredentialRotatedAtUtc = existing?.InboundCredentialRotatedAtUtc,
            InboundCredentialRevokedAtUtc = existing?.InboundCredentialRevokedAtUtc,
            InboundLastTokenIssuedAtUtc = existing?.InboundLastTokenIssuedAtUtc,
            InboundLastTokenFailedAtUtc = existing?.InboundLastTokenFailedAtUtc,
            InboundLastFailureReason = existing?.InboundLastFailureReason ?? string.Empty,
            OutboundClientId = existing?.OutboundClientId ?? string.Empty,
            OutboundKeyId = existing?.OutboundKeyId ?? string.Empty,
            ProtectedOutboundSecret = existing?.ProtectedOutboundSecret ?? string.Empty,
            OutboundRequestedScopes = existing?.OutboundRequestedScopes ?? string.Empty,
            OutboundCredentialStatus = existing?.OutboundCredentialStatus ?? ConnectionCredentialStatus.Missing,
            OutboundCredentialImportedAtUtc = existing?.OutboundCredentialImportedAtUtc,
            OutboundLastTokenReceivedAtUtc = existing?.OutboundLastTokenReceivedAtUtc,
            RegisteredAtUtc = existing?.RegisteredAtUtc ?? DateTime.UtcNow,
            LastUpdatedAtUtc = DateTime.UtcNow
        };

        if (isUpdate) await _repository.Update(entity, cancellationToken);
        else await _repository.Create(entity, cancellationToken);

        return entity.Id.ToString();
    }

    public async Task SetStatus(string runtimeNodeId, RuntimeNodeStatus status, CancellationToken cancellationToken = default)
    {
        var id = new Id(Ulid.Parse(runtimeNodeId));
        var node = await _repository.GetById(id, cancellationToken);
        if (node.IsDeleted)
        {
            throw new InvalidOperationException("Runtime node was deleted and cannot be updated.");
        }

        if (!CanTransition(node.Status, status))
        {
            throw new InvalidOperationException($"Runtime node cannot transition from {node.Status} to {status}.");
        }

        if (status == RuntimeNodeStatus.Enabled)
        {
            EnsureNodeIsConfigured(node);
        }

        await _repository.SetStatus(id, status, cancellationToken);
    }

    public Task Delete(string runtimeNodeId, CancellationToken cancellationToken = default)
        => _repository.SoftDelete(new Id(Ulid.Parse(runtimeNodeId)), cancellationToken);

    public async Task<ApplicationPagedResult<RuntimeNodeModel>> GetAll(ApplicationPagedSettings settings, CancellationToken cancellationToken = default)
    {
        var result = await _repository.GetAll(new PagedSettings(settings.PageNumber, settings.PageSize, Array.Empty<QueryFilter>(), Array.Empty<QuerySort>()), cancellationToken);
        return new ApplicationPagedResult<RuntimeNodeModel>
        {
            PageNumber = result.PageNumber,
            PageSize = result.PageSize,
            TotalRows = (int)result.TotalRows,
            TotalPages = result.TotalPages,
            Rows = result.Rows.Select(x => new RuntimeNodeModel
            {
                Id = x.Id.ToString(), Name = x.Name, Code = x.Code,
                DistributionMode = x.DistributionMode.ToString(), EndpointBaseUri = x.EndpointBaseUri, EndpointApiPath = x.EndpointApiPath,
                Status = x.Status.ToString(), IsEnabled = x.Status == RuntimeNodeStatus.Enabled, IsDeleted = x.IsDeleted, DeletedAtUtc = x.DeletedAtUtc,
                Description = x.Description, RegisteredAtUtc = x.RegisteredAtUtc,
                InboundCredentialStatus = x.InboundCredentialStatus.ToString(), OutboundCredentialStatus = x.OutboundCredentialStatus.ToString(),
                AccessTokenTtlSeconds = x.AccessTokenTtlSeconds, TokenRefreshSkewSeconds = x.TokenRefreshSkewSeconds,
                TokenValidationCacheTtlSeconds = x.TokenValidationCacheTtlSeconds, InboundClientId = x.InboundClientId,
                InboundKeyId = x.InboundKeyId, InboundAllowedScopes = x.InboundAllowedScopes,
                InboundCredentialCreatedAtUtc = x.InboundCredentialCreatedAtUtc, InboundCredentialRotatedAtUtc = x.InboundCredentialRotatedAtUtc,
                InboundCredentialRevokedAtUtc = x.InboundCredentialRevokedAtUtc, InboundLastTokenIssuedAtUtc = x.InboundLastTokenIssuedAtUtc,
                InboundLastTokenFailedAtUtc = x.InboundLastTokenFailedAtUtc, InboundLastFailureReason = x.InboundLastFailureReason,
                OutboundClientId = x.OutboundClientId, OutboundKeyId = x.OutboundKeyId, OutboundRequestedScopes = x.OutboundRequestedScopes,
                OutboundCredentialImportedAtUtc = x.OutboundCredentialImportedAtUtc, OutboundLastTokenReceivedAtUtc = x.OutboundLastTokenReceivedAtUtc,
                LastUpdatedAtUtc = x.LastUpdatedAtUtc
            }).ToArray()
        };
    }

    private static bool CanTransition(RuntimeNodeStatus current, RuntimeNodeStatus next)
        => (current, next) switch
        {
            (RuntimeNodeStatus.Pending, RuntimeNodeStatus.Suspend) => true,
            (RuntimeNodeStatus.Pending, RuntimeNodeStatus.Enabled) => true,
            (RuntimeNodeStatus.Suspend, RuntimeNodeStatus.Pending) => true,
            (RuntimeNodeStatus.Suspend, RuntimeNodeStatus.Enabled) => true,
            (RuntimeNodeStatus.Enabled, RuntimeNodeStatus.Suspend) => true,
            _ => current == next
        };

    private static void EnsureNodeIsConfigured(RuntimeNode node)
    {
        var needsOutbound = node.DistributionMode is DistributionMode.DesignPublishesToRuntime or DistributionMode.HybridSync;
        var needsInbound = node.DistributionMode is DistributionMode.RuntimeFetchesFromDesign or DistributionMode.HybridSync;

        if (needsOutbound && string.IsNullOrWhiteSpace(node.EndpointBaseUri))
        {
            throw new InvalidOperationException("Runtime endpoint is required before enabling this node.");
        }

        if (needsOutbound && node.OutboundCredentialStatus != ConnectionCredentialStatus.Active)
        {
            throw new InvalidOperationException("Runtime credentials are required before enabling this node.");
        }

        if (needsInbound && node.InboundCredentialStatus != ConnectionCredentialStatus.Active)
        {
            throw new InvalidOperationException("Design credentials are required before enabling this node.");
        }
    }
}

