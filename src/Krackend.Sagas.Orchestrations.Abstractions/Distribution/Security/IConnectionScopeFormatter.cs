namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Formats and parses artifact delivery scopes used in token requests.
/// </summary>
public interface IConnectionScopeFormatter
{
    /// <summary>
    /// Formats one scope for the wire contract.
    /// </summary>
    /// <param name="scope">Scope to format.</param>
    /// <returns>Wire scope value.</returns>
    string Format(ArtifactDeliveryScope scope);

    /// <summary>
    /// Formats multiple scopes as a space-separated value.
    /// </summary>
    /// <param name="scopes">Scopes to format.</param>
    /// <returns>Space-separated wire scope value.</returns>
    string FormatMany(IEnumerable<ArtifactDeliveryScope> scopes);

    /// <summary>
    /// Parses a space-separated wire scope value.
    /// </summary>
    /// <param name="value">Wire scope value.</param>
    /// <returns>Parsed scopes.</returns>
    IReadOnlyCollection<ArtifactDeliveryScope> ParseMany(string value);
}
