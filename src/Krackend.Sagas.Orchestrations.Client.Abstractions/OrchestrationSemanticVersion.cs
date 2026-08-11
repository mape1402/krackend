using System.Globalization;

namespace Krackend.Sagas.Orchestrations.Client.Abstractions;

/// <summary>
/// Represents a semantic version value used by orchestration client output.
/// </summary>
public readonly struct OrchestrationSemanticVersion : IComparable<OrchestrationSemanticVersion>, IEquatable<OrchestrationSemanticVersion>
{
    /// <summary>
    /// Gets the default message version.
    /// </summary>
    public static OrchestrationSemanticVersion Default { get; } = new(1, 0, 0);

    /// <summary>
    /// Initializes a new semantic version.
    /// </summary>
    public OrchestrationSemanticVersion(int major, int minor, int patch)
    {
        if (major < 0)
            throw new ArgumentOutOfRangeException(nameof(major), "Major version cannot be negative.");

        if (minor < 0)
            throw new ArgumentOutOfRangeException(nameof(minor), "Minor version cannot be negative.");

        if (patch < 0)
            throw new ArgumentOutOfRangeException(nameof(patch), "Patch version cannot be negative.");

        Major = major;
        Minor = minor;
        Patch = patch;
    }

    /// <summary>
    /// Gets the major version.
    /// </summary>
    public int Major { get; }

    /// <summary>
    /// Gets the minor version.
    /// </summary>
    public int Minor { get; }

    /// <summary>
    /// Gets the patch version.
    /// </summary>
    public int Patch { get; }

    /// <summary>
    /// Parses a semantic version from text.
    /// </summary>
    public static OrchestrationSemanticVersion Parse(string value)
    {
        if (TryParse(value, out var version))
            return version;

        throw new FormatException($"'{value}' is not a valid semantic version.");
    }

    /// <summary>
    /// Tries to parse a semantic version from text.
    /// </summary>
    public static bool TryParse(string value, out OrchestrationSemanticVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split('.');
        if (parts.Length != 3)
            return false;

        if (!TryParsePart(parts[0], out var major) ||
            !TryParsePart(parts[1], out var minor) ||
            !TryParsePart(parts[2], out var patch))
        {
            return false;
        }

        version = new OrchestrationSemanticVersion(major, minor, patch);
        return true;
    }

    /// <summary>
    /// Converts a semantic version string to an orchestration semantic version.
    /// </summary>
    public static implicit operator OrchestrationSemanticVersion(string value) => Parse(value);

    /// <inheritdoc/>
    public int CompareTo(OrchestrationSemanticVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
            return major;

        var minor = Minor.CompareTo(other.Minor);
        if (minor != 0)
            return minor;

        return Patch.CompareTo(other.Patch);
    }

    /// <inheritdoc/>
    public bool Equals(OrchestrationSemanticVersion other)
        => Major == other.Major && Minor == other.Minor && Patch == other.Patch;

    /// <inheritdoc/>
    public override bool Equals(object obj)
        => obj is OrchestrationSemanticVersion other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(Major, Minor, Patch);

    /// <summary>
    /// Compares two semantic versions for equality.
    /// </summary>
    public static bool operator ==(OrchestrationSemanticVersion left, OrchestrationSemanticVersion right)
        => left.Equals(right);

    /// <summary>
    /// Compares two semantic versions for inequality.
    /// </summary>
    public static bool operator !=(OrchestrationSemanticVersion left, OrchestrationSemanticVersion right)
        => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");

    private static bool TryParsePart(string value, out int part)
        => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out part) && part >= 0;
}
