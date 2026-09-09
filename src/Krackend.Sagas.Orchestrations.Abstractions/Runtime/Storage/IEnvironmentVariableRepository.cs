using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Persists variable values available to the runtime node.
/// </summary>
public interface IEnvironmentVariableRepository
{
    Task Upsert(EnvironmentVariableValue variable, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<EnvironmentVariableValue>> GetAll(CancellationToken cancellationToken = default);
}
