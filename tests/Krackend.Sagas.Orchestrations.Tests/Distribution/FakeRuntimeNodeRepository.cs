using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.Tests.Distribution;

internal sealed class FakeRuntimeNodeRepository : IRuntimeNodeRepository
{
    private readonly Dictionary<Id, RuntimeNode> _nodes = new();

    public Task Create(RuntimeNode runtimeNode, CancellationToken cancellationToken = default)
    {
        _nodes[runtimeNode.Id] = runtimeNode;
        return Task.CompletedTask;
    }

    public Task Update(RuntimeNode runtimeNode, CancellationToken cancellationToken = default)
    {
        _nodes[runtimeNode.Id] = runtimeNode;
        return Task.CompletedTask;
    }

    public Task SetStatus(Id runtimeNodeId, RuntimeNodeStatus status, CancellationToken cancellationToken = default)
    {
        if (_nodes.TryGetValue(runtimeNodeId, out var node))
        {
            node.Status = status;
            node.IsEnabled = status == RuntimeNodeStatus.Enabled;
        }

        return Task.CompletedTask;
    }

    public Task SoftDelete(Id runtimeNodeId, CancellationToken cancellationToken = default)
    {
        if (_nodes.TryGetValue(runtimeNodeId, out var node))
        {
            node.Status = RuntimeNodeStatus.Suspend;
            node.IsEnabled = false;
            node.IsDeleted = true;
            node.DeletedAtUtc = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task<RuntimeNode> GetById(Id runtimeNodeId, CancellationToken cancellationToken = default)
        => Task.FromResult(_nodes.GetValueOrDefault(runtimeNodeId)!);

    public Task<RuntimeNode> GetByCode(string code, CancellationToken cancellationToken = default)
        => Task.FromResult(_nodes.Values.FirstOrDefault(x => !x.IsDeleted && string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))!);

    public Task<RuntimeNode> GetByInboundClientId(string clientId, CancellationToken cancellationToken = default)
        => Task.FromResult(_nodes.Values.FirstOrDefault(x => !x.IsDeleted && string.Equals(x.InboundClientId, clientId, StringComparison.Ordinal))!);

    public Task<PagedResult<RuntimeNode>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default)
    {
        var rows = _nodes.Values.Where(x => !x.IsDeleted).ToArray();
        return Task.FromResult(new PagedResult<RuntimeNode>(1, 1, rows.Length, rows.Length, rows));
    }
}
