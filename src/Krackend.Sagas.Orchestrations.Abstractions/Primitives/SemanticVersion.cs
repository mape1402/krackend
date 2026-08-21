namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a semantic version value (major.minor.patch).
/// </summary>
[JsonConverter(typeof(SemanticVersionJsonConverter))]
public readonly struct SemanticVersion : IComparable<SemanticVersion>
{
    /// <summary>
    /// Initializes a new semantic version.
    /// </summary>
    public SemanticVersion(int major, int minor, int patch)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    /// <summary>
    /// Gets major.
    /// </summary>
    public int Major { get; }

    /// <summary>
    /// Gets minor.
    /// </summary>
    public int Minor { get; }

    /// <summary>
    /// Gets patch.
    /// </summary>
    public int Patch { get; }

    /// <summary>
    /// Compares the current instance with another instance.
    /// </summary>
    public int CompareTo(SemanticVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
            return major;

        var minor = Minor.CompareTo(other.Minor);
        if (minor != 0)
            return minor;

        return Patch.CompareTo(other.Patch);
    }

    /// <summary>
    /// Returns the string representation of the current instance.
    /// </summary>
    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
