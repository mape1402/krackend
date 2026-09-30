namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using System.Text.RegularExpressions;

/// <summary>
/// Provides reusable validation helpers for interaction requests.
/// </summary>
internal static class ValidationRules
{
    private static readonly Regex OrchestratorKeyExpression = new(
        "^[A-Za-z][A-Za-z0-9]*(?:[._][A-Za-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Determines whether the value is a valid ULID.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public static bool IsUlid(string value)
    {
        return Ulid.TryParse(value, out _);
    }

    /// <summary>
    /// Determines whether the value is a valid semantic version.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public static bool IsSemanticVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] parts = value.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        return int.TryParse(parts[0], out _)
            && int.TryParse(parts[1], out _)
            && int.TryParse(parts[2], out _);
    }

    /// <summary>
    /// Determines whether the value is a valid orchestrator component key.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <returns>True when the key uses alphanumeric segments separated only by dot or underscore.</returns>
    public static bool IsOrchestratorKey(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            OrchestratorKeyExpression.IsMatch(value);
    }
}


