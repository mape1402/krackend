using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;

namespace Krackend.Sagas.Orchestrations.Tests.Distribution;

internal sealed class FakeRuntimeDesignNodeRepository : IRuntimeDesignNodeRepository
{
    private readonly Dictionary<Id, RuntimeDesignNode> _nodes = new();

    public Task<IReadOnlyCollection<RuntimeDesignNode>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<RuntimeDesignNode>>(_nodes.Values.ToArray());

    public IReadOnlyCollection<RuntimeDesignNode> GetEnabled()
        => _nodes.Values.Where(x => x.IsEnabled).ToArray();

    public Task<RuntimeDesignNode> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
        => Task.FromResult(_nodes.GetValueOrDefault(id)!);

    public Task<RuntimeDesignNode> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_nodes.Values.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))!);

    public Task<RuntimeDesignNode> GetByInboundClientIdAsync(string clientId, CancellationToken cancellationToken = default)
        => Task.FromResult(_nodes.Values.FirstOrDefault(x => string.Equals(x.InboundClientId, clientId, StringComparison.Ordinal))!);

    public Task UpsertAsync(RuntimeDesignNode designNode, CancellationToken cancellationToken = default)
    {
        _nodes[designNode.Id] = designNode;
        return Task.CompletedTask;
    }

    public Task SetEnabledAsync(Id id, bool isEnabled, CancellationToken cancellationToken = default)
    {
        if (_nodes.TryGetValue(id, out var node))
        {
            node.IsEnabled = isEnabled;
        }

        return Task.CompletedTask;
    }
}
