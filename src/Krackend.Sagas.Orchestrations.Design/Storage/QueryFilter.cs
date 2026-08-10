namespace Krackend.Sagas.Orchestrations.Design.Storage;

/// <summary>
/// Represents one filter expression used to constrain query results.
/// </summary>
public sealed record QueryFilter(
    string Field,
    string Operator,
    string Value);

