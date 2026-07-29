using System.Text.RegularExpressions;

namespace Krackend.EventSourcing.Contracts;

/// <summary>
/// Represents a semantic version with major, minor, and patch components.
/// </summary>
public readonly struct SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
{
    private static readonly Regex SemVerRegex = new(
        @"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of the <see cref="SemanticVersion"/> struct.
    /// </summary>
    public SemanticVersion(int major, int minor, int patch)
    {
        if (major < 0)
            throw new ArgumentOutOfRangeException(nameof(major));

        if (minor < 0)
            throw new ArgumentOutOfRangeException(nameof(minor));

        if (patch < 0)
            throw new ArgumentOutOfRangeException(nameof(patch));

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
    /// Gets the default schema version.
    /// </summary>
    public static SemanticVersion Default => new(1, 0, 0);

    /// <summary>
    /// Attempts to parse a semantic version string.
    /// </summary>
    public static bool TryParse(string? version, out SemanticVersion semVer)
    {
        semVer = default;

        if (string.IsNullOrWhiteSpace(version))
            return false;

        var match = SemVerRegex.Match(version.Trim());
        if (!match.Success)
            return false;

        if (!int.TryParse(match.Groups["major"].Value, out var major))
            return false;

        if (!int.TryParse(match.Groups["minor"].Value, out var minor))
            return false;

        if (!int.TryParse(match.Groups["patch"].Value, out var patch))
            return false;

        semVer = new SemanticVersion(major, minor, patch);
        return true;
    }

    /// <summary>
    /// Parses a semantic version string.
    /// </summary>
    public static SemanticVersion Parse(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentNullException(nameof(version));

        if (!TryParse(version, out var semVer))
            throw new FormatException("Invalid Semantic Version format.");

        return semVer;
    }

    /// <inheritdoc />
    public int CompareTo(SemanticVersion other)
    {
        var result = Major.CompareTo(other.Major);
        if (result != 0)
            return result;

        result = Minor.CompareTo(other.Minor);
        if (result != 0)
            return result;

        return Patch.CompareTo(other.Patch);
    }

    /// <inheritdoc />
    public bool Equals(SemanticVersion other)
        => Major == other.Major
            && Minor == other.Minor
            && Patch == other.Patch;

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is SemanticVersion other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(Major, Minor, Patch);

    /// <inheritdoc />
    public override string ToString()
        => $"{Major}.{Minor}.{Patch}";

    /// <summary>
    /// Compares two versions for equality.
    /// </summary>
    public static bool operator ==(SemanticVersion left, SemanticVersion right)
        => left.Equals(right);

    /// <summary>
    /// Compares two versions for inequality.
    /// </summary>
    public static bool operator !=(SemanticVersion left, SemanticVersion right)
        => !(left == right);

    /// <summary>
    /// Returns true when the left version is lower than the right version.
    /// </summary>
    public static bool operator <(SemanticVersion left, SemanticVersion right)
        => left.CompareTo(right) < 0;

    /// <summary>
    /// Returns true when the left version is lower than or equal to the right version.
    /// </summary>
    public static bool operator <=(SemanticVersion left, SemanticVersion right)
        => left.CompareTo(right) <= 0;

    /// <summary>
    /// Returns true when the left version is greater than the right version.
    /// </summary>
    public static bool operator >(SemanticVersion left, SemanticVersion right)
        => left.CompareTo(right) > 0;

    /// <summary>
    /// Returns true when the left version is greater than or equal to the right version.
    /// </summary>
    public static bool operator >=(SemanticVersion left, SemanticVersion right)
        => left.CompareTo(right) >= 0;

    /// <summary>
    /// Converts a string into a semantic version.
    /// </summary>
    public static implicit operator SemanticVersion(string source)
        => Parse(source);

    /// <summary>
    /// Converts a semantic version into its string representation.
    /// </summary>
    public static implicit operator string(SemanticVersion version)
        => version.ToString();
}
