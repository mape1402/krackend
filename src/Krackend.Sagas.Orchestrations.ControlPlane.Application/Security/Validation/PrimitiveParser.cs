namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Parses primitive values used by the interaction layer.
/// </summary>
public static class PrimitiveParser
{
    /// <summary>
    /// Parses an id from ULID text.
    /// </summary>
    /// <param name="value">Id text.</param>
    /// <returns>Parsed id.</returns>
    public static Id ParseId(string value)
    {
        return new Id(Ulid.Parse(value));
    }
}
