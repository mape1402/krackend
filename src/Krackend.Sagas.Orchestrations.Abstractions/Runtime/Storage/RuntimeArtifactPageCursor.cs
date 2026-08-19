namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Represents the current cursor for paged runtime artifact reads.
/// </summary>
public sealed record RuntimeArtifactPageCursor(int Offset)
{
    /// <summary>
    /// Gets the first page cursor.
    /// </summary>
    public static RuntimeArtifactPageCursor First { get; } = new(0);
}
