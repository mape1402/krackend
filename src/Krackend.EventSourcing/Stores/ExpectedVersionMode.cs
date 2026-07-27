namespace Krackend.EventSourcing.Stores;

/// <summary>
/// Defines supported append precondition modes.
/// </summary>
public enum ExpectedVersionMode
{
    /// <summary>
    /// No stream version precondition is required.
    /// </summary>
    Any,

    /// <summary>
    /// The stream must not exist.
    /// </summary>
    NoStream,

    /// <summary>
    /// The stream must match an exact version.
    /// </summary>
    Exact
}
