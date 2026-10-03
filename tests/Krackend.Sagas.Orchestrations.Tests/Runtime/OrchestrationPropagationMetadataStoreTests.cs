namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Runtime.Metadata;

public sealed class OrchestrationPropagationMetadataStoreTests
{
    [Fact]
    public void SaveStoresClonedEnvelopeAndLoadReturnsClonedMetadata()
    {
        var store = new DefaultOrchestrationPropagationMetadataStore();
        var instance = CreateInstance();
        var metadata = new OrchestrationPropagationMetadata
        {
            Items =
            {
                ["audit.context"] = JsonNode.Parse("""{"requestId":"req-1"}""")!
            }
        };

        store.Save(instance, metadata);
        ((JsonObject)metadata.Items["audit.context"]!)["requestId"] = "mutated";

        var loaded = store.Load(instance);
        ((JsonObject)loaded.Items["audit.context"]!)["requestId"] = "loaded-mutated";
        var loadedAgain = store.Load(instance);

        Assert.Equal("req-1", loadedAgain.Items["audit.context"]!["requestId"]!.GetValue<string>());
    }

    [Fact]
    public void SaveRemovesPropagationMetadataWhenInputIsEmpty()
    {
        var store = new DefaultOrchestrationPropagationMetadataStore();
        var instance = CreateInstance();
        instance.Metadata[OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey] =
            JsonNode.Parse("""{"items":{"audit.context":{"requestId":"stale"}}}""")!;

        store.Save(instance, null!);

        Assert.False(instance.Metadata.ContainsKey(OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey));
        Assert.Throws<ArgumentNullException>(() => store.Save(null!, new OrchestrationPropagationMetadata()));
    }

    [Fact]
    public void LoadReturnsEmptyMetadataWhenInstanceOrStoredEnvelopeIsMissing()
    {
        var store = new DefaultOrchestrationPropagationMetadataStore();
        var instance = CreateInstance();
        instance.Metadata = null!;

        Assert.False(store.Load(null!).HasItems);
        Assert.False(store.Load(instance).HasItems);
        instance.Metadata = [];
        Assert.False(store.Load(instance).HasItems);
        instance.Metadata[OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey] = null!;
        Assert.False(store.Load(instance).HasItems);
    }

    [Fact]
    public void LoadFallsBackToRawItemsWhenEnvelopeShapeIsInvalid()
    {
        var store = new DefaultOrchestrationPropagationMetadataStore();
        var instance = CreateInstance();
        instance.Metadata[OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey] =
            JsonNode.Parse("""{"items":[1],"audit.context":{"requestId":"req-items"}}""")!;

        var loaded = store.Load(instance);

        Assert.Equal("req-items", loaded.Items["audit.context"]!["requestId"]!.GetValue<string>());
    }

    [Fact]
    public void LoadReturnsEmptyMetadataWhenEnvelopeAndItemsCannotBeRead()
    {
        var store = new DefaultOrchestrationPropagationMetadataStore();
        var instance = CreateInstance();
        instance.Metadata[OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey] =
            JsonNode.Parse("""[1]""")!;

        var loaded = store.Load(instance);

        Assert.False(loaded.HasItems);

        instance.Metadata[OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey] =
            JsonValue.Create("not-an-envelope")!;

        var scalar = store.Load(instance);

        Assert.False(scalar.HasItems);
    }

    private static OrchestrationInstance CreateInstance()
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "orders",
            RuntimeOrchestrationArtifactId = Id.New(),
            TriggerIntakeId = Id.New(),
            CorrelationId = "corr-1",
            SagaId = "saga-1",
            ExecutionKey = "orders:corr-1",
            Status = OrchestrationInstanceStatus.Running,
            StartedOnUtc = DateTime.UtcNow,
            LastUpdatedOnUtc = DateTime.UtcNow,
            SnapshotPayload = JsonNode.Parse("""{"orderId":"A1"}""")
        };
}
