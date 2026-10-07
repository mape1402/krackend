namespace Krackend.Sagas.Orchestrations.Runtime.Operations;

/// <summary>
/// Provides the current runtime operational state.
/// </summary>
public interface IRuntimeOperationalStateProvider
{
    /// <summary>
    /// Gets the latest known runtime operational snapshot.
    /// </summary>
    RuntimeOperationalSnapshot Current { get; }
}
