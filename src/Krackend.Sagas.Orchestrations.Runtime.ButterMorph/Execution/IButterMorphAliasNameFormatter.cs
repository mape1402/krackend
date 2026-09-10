namespace Krackend.Sagas.Orchestrations.Runtime.ButterMorph;

/// <summary>
/// Formats orchestration keys so they can be consumed as ButterMorph aliases or path members.
/// </summary>
public interface IButterMorphAliasNameFormatter
{
    /// <summary>
    /// Formats a stage or task key into a stable ButterMorph-friendly name.
    /// </summary>
    /// <param name="value">The orchestration key to format.</param>
    /// <returns>A sanitized name usable in ButterMorph DSL paths.</returns>
    string Format(string value);
}
