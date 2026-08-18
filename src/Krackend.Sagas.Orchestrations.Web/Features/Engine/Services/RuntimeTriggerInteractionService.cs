using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;

namespace Krackend.Sagas.Orchestrations.Web;

public sealed class RuntimeTriggerInteractionService : IRuntimeTriggerInteractionService
{
    private readonly RuntimeEnvironmentDescriptor _runtimeEnvironment;
    private readonly ITriggerIntakeBuffer _intakeBuffer;
    private readonly IRuntimeEngine _runtimeEngine;
    private readonly IRuntimeDurableWorkScheduler _durableWorkScheduler;

    public RuntimeTriggerInteractionService(
        RuntimeEnvironmentDescriptor runtimeEnvironment,
        ITriggerIntakeBuffer intakeBuffer,
        IRuntimeEngine runtimeEngine,
        IRuntimeDurableWorkScheduler durableWorkScheduler)
    {
        _runtimeEnvironment = runtimeEnvironment ?? throw new ArgumentNullException(nameof(runtimeEnvironment));
        _intakeBuffer = intakeBuffer ?? throw new ArgumentNullException(nameof(intakeBuffer));
        _runtimeEngine = runtimeEngine ?? throw new ArgumentNullException(nameof(runtimeEngine));
        _durableWorkScheduler = durableWorkScheduler ?? throw new ArgumentNullException(nameof(durableWorkScheduler));
    }

    public async Task<RuntimeTriggerResult> Enqueue(
        RuntimeTriggerRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return new RuntimeTriggerResult
            {
                Accepted = false,
                Status = "Rejected",
                Message = validationError
            };
        }

        var payload = JsonNode.Parse(request.PayloadJson.Trim());
        var ingress = new RuntimeIngressEnvelope
        {
            IngressId = Id.New().ToString(),
            Kind = RuntimeIngressKind.Trigger,
            EnvironmentKey = request.EnvironmentKey.Trim(),
            OrchestrationName = request.TriggerKey.Trim(),
            OrchestrationVersion = request.ArtifactVersion?.Trim(),
            CorrelationId = request.CorrelationId?.Trim(),
            IdempotencyKey = BuildVersionScopedIdempotencyKey(request.IdempotencyKey, request.ArtifactVersion),
            Source = new RuntimeTransportDescriptor
            {
                Kind = RuntimeTransportKind.Http,
                Address = "/runtime/triggers",
                MessageId = request.SourceMessageId?.Trim()
            },
            Payload = payload,
            ReceivedOnUtc = DateTime.UtcNow
        };

        var actionId = await _durableWorkScheduler.ScheduleProcessIngress(ingress, cancellationToken);
        return new RuntimeTriggerResult
        {
            Accepted = true,
            BufferItemId = actionId.ToString(),
            Status = "Accepted",
            Message = "Trigger accepted into runtime durable ingress."
        };
    }

    public Task<RuntimeEngineProcessResult> ProcessNext(CancellationToken cancellationToken = default)
        => _runtimeEngine.ProcessNext(cancellationToken);

    public Task<IReadOnlyCollection<RuntimeEngineProcessResult>> ProcessAll(
        int maxItems,
        CancellationToken cancellationToken = default)
        => _runtimeEngine.ProcessAll(maxItems, cancellationToken);

    private string Validate(RuntimeTriggerRequest request)
    {
        if (request is null)
        {
            return "Trigger request is required.";
        }

        if (string.IsNullOrWhiteSpace(request.TriggerKey))
        {
            return "TriggerKey is required.";
        }

        if (string.IsNullOrWhiteSpace(request.EnvironmentKey))
        {
            return "EnvironmentKey is required.";
        }

        if (!string.Equals(request.EnvironmentKey.Trim(), _runtimeEnvironment.EnvironmentKey, StringComparison.Ordinal))
        {
            return $"Trigger target environment '{request.EnvironmentKey}' does not match runtime environment '{_runtimeEnvironment.EnvironmentKey}'.";
        }

        if (string.IsNullOrWhiteSpace(request.PayloadJson))
        {
            return "PayloadJson is required.";
        }

        try
        {
            _ = JsonNode.Parse(request.PayloadJson);
            _ = ParseTriggerType(request.TriggerType);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return ex.Message;
        }

        return null;
    }

    private static TriggerType ParseTriggerType(string value)
        => string.IsNullOrWhiteSpace(value)
            ? TriggerType.Event
            : Enum.TryParse<TriggerType>(value, true, out var parsed)
                ? parsed
                : throw new ArgumentException($"TriggerType '{value}' is not supported.");

    private static string BuildVersionScopedIdempotencyKey(string idempotencyKey, string artifactVersion)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return null;

        var trimmed = idempotencyKey.Trim();
        return string.IsNullOrWhiteSpace(artifactVersion)
            ? trimmed
            : $"{trimmed}::artifact-version:{artifactVersion.Trim()}";
    }
}
