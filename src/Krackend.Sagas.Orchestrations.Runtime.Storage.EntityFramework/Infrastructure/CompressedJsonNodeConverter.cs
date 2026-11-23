using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

/// <summary>
/// Compresses large opaque JSON documents while continuing to read legacy plain JSON.
/// </summary>
internal sealed class CompressedJsonNodeConverter : ValueConverter<JsonNode, string>
{
    private const string Prefix = "br1:";
    private const int CompressionThresholdBytes = 8 * 1024;

    public CompressedJsonNodeConverter()
        : base(
            node => node == null ? null : Compress(node),
            value => string.IsNullOrWhiteSpace(value) ? null : Decompress(value))
    {
    }

    private static string Compress(JsonNode node)
    {
        var json = node.ToJsonString();
        var source = Encoding.UTF8.GetBytes(json);
        // Small snapshots dominate real-time transitions. Brotli setup, buffers and
        // Base64 allocation cost more than the bytes saved and used to run on every
        // callback. Keep them as plain JSON; the reader already supports both forms.
        if (source.Length < CompressionThresholdBytes)
            return json;

        using var output = new MemoryStream();
        using (var compressor = new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true))
            compressor.Write(source);
        return Prefix + Convert.ToBase64String(output.ToArray());
    }

    private static JsonNode Decompress(string value)
    {
        if (!value.StartsWith(Prefix, StringComparison.Ordinal))
            return JsonNode.Parse(value);

        try
        {
            var bytes = Convert.FromBase64String(value[Prefix.Length..]);
            using var input = new MemoryStream(bytes, writable: false);
            using var decompressor = new BrotliStream(input, CompressionMode.Decompress);
            return JsonNode.Parse(decompressor);
        }
        catch (Exception error) when (error is FormatException or InvalidDataException or JsonException)
        {
            throw new InvalidOperationException("The compressed orchestration snapshot is invalid.", error);
        }
    }
}
