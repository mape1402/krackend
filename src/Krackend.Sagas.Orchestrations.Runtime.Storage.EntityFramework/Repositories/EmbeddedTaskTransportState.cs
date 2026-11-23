using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

/// <summary>
/// Serializes the live transport projection into the canonical task metadata. The
/// task row remains the source of truth; attempt and dispatch objects are rebuilt for
/// the existing engine/recovery contracts without relational child rows.
/// </summary>
internal static class EmbeddedTaskTransportState
{
    internal const string MetadataKey = "Runtime.Transport.Current";
    private const string CompressedPrefix = "br1:";
    private static readonly ConditionalWeakTable<TaskExecution, CachedRoot> DecodedRoots = new();

    internal static bool IsEmbedded(TaskExecution task)
        => task?.Metadata?.TryGetValue(MetadataKey, out var value) == true &&
            (value is JsonObject || value is JsonValue);

    internal static void StoreAttempt(TaskExecution task, TaskExecutionAttempt attempt)
    {
        var root = GetRoot(task);
        root["attempt"] = new JsonObject
        {
            ["attemptNumber"] = attempt.AttemptNumber,
            ["status"] = attempt.Status.ToString(),
            ["startedOnUtc"] = Format(attempt.StartedOnUtc),
            ["waitingSinceUtc"] = Format(attempt.WaitingSinceUtc),
            ["completedOnUtc"] = Format(attempt.CompletedOnUtc),
            ["failedOnUtc"] = Format(attempt.FailedOnUtc),
            ["timedOutOnUtc"] = Format(attempt.TimedOutOnUtc),
            ["requestPayload"] = attempt.RequestPayload?.DeepClone(),
            ["responsePayload"] = attempt.ResponsePayload?.DeepClone(),
            ["errorCode"] = attempt.ErrorCode,
            ["errorMessage"] = attempt.ErrorMessage,
            ["metadata"] = ToJson(attempt.Metadata)
        };
        SetRoot(task, root);
    }

    internal static void StoreDispatch(TaskExecution task, TaskDispatch dispatch)
    {
        var root = GetRoot(task);
        root["dispatch"] = new JsonObject
        {
            ["dispatchType"] = dispatch.DispatchType,
            ["destination"] = dispatch.Destination,
            ["requestPayload"] = dispatch.RequestPayload?.DeepClone(),
            ["dispatchStatus"] = dispatch.DispatchStatus,
            ["commandId"] = dispatch.CommandId,
            ["correlationId"] = dispatch.CorrelationId,
            ["sentOnUtc"] = Format(dispatch.SentOnUtc),
            ["callbackReceiptId"] = dispatch.CallbackReceiptId,
            ["callbackAcceptedOnUtc"] = Format(dispatch.CallbackAcceptedOnUtc),
            ["pendingCallbackPayload"] = dispatch.PendingCallbackPayload,
            ["callbackPending"] = dispatch.CallbackPending,
            ["scheduledOnUtc"] = Format(dispatch.ScheduledOnUtc),
            ["acknowledgedOnUtc"] = Format(dispatch.AcknowledgedOnUtc),
            ["failedOnUtc"] = Format(dispatch.FailedOnUtc),
            ["failureReason"] = dispatch.FailureReason,
            ["physicalCancellationPending"] = dispatch.PhysicalCancellationPending,
            ["physicalCancellationAttemptCount"] = dispatch.PhysicalCancellationAttemptCount,
            ["physicalCancellationLastAttemptOnUtc"] = Format(dispatch.PhysicalCancellationLastAttemptOnUtc),
            ["physicalCancellationCompletedOnUtc"] = Format(dispatch.PhysicalCancellationCompletedOnUtc),
            ["metadata"] = ToJson(dispatch.Metadata)
        };
        SetRoot(task, root);
    }

    internal static bool TryReadAttempt(TaskExecution task, out TaskExecutionAttempt attempt)
    {
        attempt = null;
        if (!TryGetSection(task, "attempt", out var value)) return false;
        attempt = new TaskExecutionAttempt
        {
            Id = task.Id,
            TaskExecutionId = task.Id,
            AttemptNumber = ReadInt(value, "attemptNumber", task.LastAttemptNumber),
            Status = ReadEnum(value, "status", task.Status),
            StartedOnUtc = ReadDate(value, "startedOnUtc"),
            WaitingSinceUtc = ReadDate(value, "waitingSinceUtc"),
            CompletedOnUtc = ReadDate(value, "completedOnUtc"),
            FailedOnUtc = ReadDate(value, "failedOnUtc"),
            TimedOutOnUtc = ReadDate(value, "timedOutOnUtc"),
            RequestPayload = value["requestPayload"]?.DeepClone(),
            ResponsePayload = value["responsePayload"]?.DeepClone(),
            ErrorCode = ReadString(value, "errorCode"),
            ErrorMessage = ReadString(value, "errorMessage"),
            DispatchId = task.Id,
            Metadata = ReadMetadata(value["metadata"])
        };
        return true;
    }

    internal static bool TryReadDispatch(TaskExecution task, out TaskDispatch dispatch)
    {
        dispatch = null;
        if (!TryGetSection(task, "dispatch", out var value)) return false;
        dispatch = new TaskDispatch
        {
            Id = task.Id,
            TaskExecutionAttemptId = task.Id,
            DispatchType = ReadString(value, "dispatchType") ?? string.Empty,
            Destination = ReadString(value, "destination"),
            RequestPayload = value["requestPayload"]?.DeepClone(),
            DispatchStatus = ReadString(value, "dispatchStatus") ?? "WaitingResponse",
            CommandId = ReadString(value, "commandId"),
            CorrelationId = ReadString(value, "correlationId"),
            SentOnUtc = ReadDate(value, "sentOnUtc"),
            CallbackReceiptId = ReadString(value, "callbackReceiptId"),
            CallbackAcceptedOnUtc = ReadDate(value, "callbackAcceptedOnUtc"),
            PendingCallbackPayload = ReadString(value, "pendingCallbackPayload"),
            CallbackPending = ReadBool(value, "callbackPending"),
            ScheduledOnUtc = ReadDate(value, "scheduledOnUtc"),
            AcknowledgedOnUtc = ReadDate(value, "acknowledgedOnUtc"),
            FailedOnUtc = ReadDate(value, "failedOnUtc"),
            FailureReason = ReadString(value, "failureReason"),
            PhysicalCancellationPending = ReadBool(value, "physicalCancellationPending"),
            PhysicalCancellationAttemptCount = ReadInt(value, "physicalCancellationAttemptCount", 0),
            PhysicalCancellationLastAttemptOnUtc = ReadDate(value, "physicalCancellationLastAttemptOnUtc"),
            PhysicalCancellationCompletedOnUtc = ReadDate(value, "physicalCancellationCompletedOnUtc"),
            Metadata = ReadMetadata(value["metadata"])
        };
        return true;
    }

    private static JsonObject GetRoot(TaskExecution task)
    {
        if (DecodedRoots.TryGetValue(task, out var cached)) return cached.Value;
        if (!task.Metadata.TryGetValue(MetadataKey, out var value)) return new JsonObject();
        if (value is JsonObject root) return root;
        if (!TryDecompress(value, out var compressedRoot)) return new JsonObject();
        CacheRoot(task, compressedRoot);
        return compressedRoot;
    }

    private static void SetRoot(TaskExecution task, JsonObject root)
    {
        var metadata = task.Metadata.ToDictionary(
            pair => pair.Key,
            pair => pair.Value?.DeepClone(),
            StringComparer.Ordinal);
        metadata[MetadataKey] = JsonValue.Create(Compress(root));
        task.Metadata = metadata;
        CacheRoot(task, root);
    }

    private static bool TryGetSection(TaskExecution task, string name, out JsonObject value)
    {
        value = null;
        if (task?.Metadata?.TryGetValue(MetadataKey, out var rootValue) != true) return false;
        JsonObject root;
        if (DecodedRoots.TryGetValue(task, out var cached)) root = cached.Value;
        else if (rootValue is JsonObject expanded) root = expanded;
        else if (!TryDecompress(rootValue, out root)) return false;
        else CacheRoot(task, root);
        return root[name] is JsonObject section &&
            (value = section) is not null;
    }

    private static string Compress(JsonObject root)
    {
        var source = Encoding.UTF8.GetBytes(root.ToJsonString());
        using var output = new MemoryStream();
        using (var compressor = new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true))
            compressor.Write(source);
        return CompressedPrefix + Convert.ToBase64String(output.ToArray());
    }

    private static void CacheRoot(TaskExecution task, JsonObject root)
    {
        DecodedRoots.Remove(task);
        DecodedRoots.Add(task, new CachedRoot(root));
    }

    private sealed record CachedRoot(JsonObject Value);

    private static bool TryDecompress(JsonNode value, out JsonObject root)
    {
        root = null;
        string encoded;
        try { encoded = value?.GetValue<string>(); }
        catch (InvalidOperationException) { return false; }
        if (string.IsNullOrWhiteSpace(encoded) || !encoded.StartsWith(CompressedPrefix, StringComparison.Ordinal))
            return false;
        try
        {
            var bytes = Convert.FromBase64String(encoded[CompressedPrefix.Length..]);
            using var input = new MemoryStream(bytes, writable: false);
            using var decompressor = new BrotliStream(input, CompressionMode.Decompress);
            return JsonNode.Parse(decompressor) is JsonObject parsed && (root = parsed) is not null;
        }
        catch (Exception error) when (error is FormatException or InvalidDataException or JsonException)
        {
            return false;
        }
    }

    private static JsonObject ToJson(IReadOnlyDictionary<string, JsonNode> metadata)
    {
        var result = new JsonObject();
        if (metadata is null) return result;
        foreach (var pair in metadata) result[pair.Key] = pair.Value?.DeepClone();
        return result;
    }

    private static Dictionary<string, JsonNode> ReadMetadata(JsonNode node)
    {
        if (node is not JsonObject value) return new Dictionary<string, JsonNode>();
        return value.ToDictionary(pair => pair.Key, pair => pair.Value?.DeepClone(), StringComparer.Ordinal);
    }

    private static JsonNode Format(DateTime? value)
        => value.HasValue ? JsonValue.Create(value.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)) : null;

    private static DateTime? ReadDate(JsonObject value, string name)
        => DateTime.TryParse(ReadString(value, name), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;

    private static string ReadString(JsonObject value, string name)
    {
        try { return value[name]?.GetValue<string>(); }
        catch (InvalidOperationException) { return null; }
    }

    private static int ReadInt(JsonObject value, string name, int fallback)
    {
        try { return value[name]?.GetValue<int>() ?? fallback; }
        catch (InvalidOperationException) { return fallback; }
    }

    private static bool ReadBool(JsonObject value, string name)
    {
        try { return value[name]?.GetValue<bool>() ?? false; }
        catch (InvalidOperationException) { return false; }
    }

    private static T ReadEnum<T>(JsonObject value, string name, T fallback) where T : struct
        => Enum.TryParse<T>(ReadString(value, name), true, out var parsed) ? parsed : fallback;
}
