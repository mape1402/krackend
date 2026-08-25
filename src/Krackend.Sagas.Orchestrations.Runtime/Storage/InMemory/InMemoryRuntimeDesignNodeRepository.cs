using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory
{
    /// <summary>
    /// Stores runtime design nodes in memory for local runtime scenarios.
    /// </summary>
    internal sealed class InMemoryRuntimeDesignNodeRepository : IRuntimeDesignNodeRepository
    {
        private readonly InMemoryRuntimeStore _store;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryRuntimeDesignNodeRepository"/> class.
        /// </summary>
        public InMemoryRuntimeDesignNodeRepository(InMemoryRuntimeStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <inheritdoc />
        public Task<IReadOnlyCollection<RuntimeDesignNode>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<RuntimeDesignNode>>(
                _store.DesignNodes.Values
                    .OrderBy(x => x.Name)
                    .ThenBy(x => x.Key)
                    .ToArray());

        /// <inheritdoc />
        public IReadOnlyCollection<RuntimeDesignNode> GetEnabled()
            => _store.DesignNodes.Values
                .Where(x => x.IsEnabled)
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Key)
                .ToArray();

        /// <inheritdoc />
        public Task<RuntimeDesignNode> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
        {
            _store.DesignNodes.TryGetValue(id, out var designNode);
            return Task.FromResult(designNode);
        }

        /// <inheritdoc />
        public Task<RuntimeDesignNode> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        {
            var designNode = _store.DesignNodes.Values.FirstOrDefault(x =>
                string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(designNode);
        }

        /// <inheritdoc />
        public Task UpsertAsync(RuntimeDesignNode designNode, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(designNode);
            _store.DesignNodes.AddOrUpdate(designNode.Id, designNode, (_, _) => designNode);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task SetEnabledAsync(Id id, bool isEnabled, CancellationToken cancellationToken = default)
        {
            if (_store.DesignNodes.TryGetValue(id, out var designNode))
            {
                designNode.IsEnabled = isEnabled;
                designNode.UpdatedOnUtc = DateTime.UtcNow;
            }

            return Task.CompletedTask;
        }
    }
}
