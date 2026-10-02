namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Extensions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

internal sealed class RuntimeExtensionManifestConverter : ValueConverter<KrackendExtensionManifest, string>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public RuntimeExtensionManifestConverter()
        : base(
            manifest => JsonSerializer.Serialize(manifest, SerializerOptions),
            payload => JsonSerializer.Deserialize<KrackendExtensionManifest>(payload, SerializerOptions))
    {
    }
}
