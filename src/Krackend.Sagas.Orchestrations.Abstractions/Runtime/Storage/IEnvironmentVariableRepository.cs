using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Persists environment variable values available to the runtime.
/// </summary>
public interface IEnvironmentVariableRepository
{
    Task Upsert(EnvironmentVariableValue variable, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<EnvironmentVariableValue>> GetByEnvironmentKey(
        string environmentKey,
        CancellationToken cancellationToken = default);
}
