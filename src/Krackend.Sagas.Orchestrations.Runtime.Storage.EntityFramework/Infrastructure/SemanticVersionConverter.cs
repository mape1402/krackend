using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

internal sealed class SemanticVersionConverter : ValueConverter<SemanticVersion, string>
{
    public SemanticVersionConverter()
        : base(
            version => version.ToString(),
            value => Parse(value))
    {
    }

    private static SemanticVersion Parse(string value)
    {
        var parts = value.Split('.');
        return new SemanticVersion(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
    }
}
