namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Represents the result of processing a compensation execution.
/// </summary>
/// <param name="Succeeded">Whether the compensation was processed successfully.</param>
/// <param name="Status">Resulting compensation status.</param>
/// <param name="Message">Result message.</param>
public sealed record RuntimeCompensationExecutionResult(
    bool Succeeded,
    string Status,
    string Message);
