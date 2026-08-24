namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

/// <summary>
/// Provides common validation predicates.
/// </summary>
public static class ValidationRules
{
    /// <summary>
    /// Returns whether input is a valid ULID.
    /// </summary>
    /// <param name="value">Input string.</param>
    /// <returns>True when valid.</returns>
    public static bool IsUlid(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && Ulid.TryParse(value, out _);
    }
}
