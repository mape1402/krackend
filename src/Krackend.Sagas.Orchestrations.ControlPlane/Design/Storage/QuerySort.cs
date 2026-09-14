namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

/// <summary>
/// Represents one sort expression used to order query results.
/// </summary>
public sealed record QuerySort(
    string Field,
    bool Descending);

