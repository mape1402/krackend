namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Provides reusable validation helpers for interaction requests.
/// </summary>
internal static class ValidationRules
{
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
}


