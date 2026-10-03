using System.Text.Json;
using global::ButterMorph.Abstractions;
using global::ButterMorph.SchemaDesign;
using global::ButterMorph.Web.Razor;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class MetadataDescriptorButterMorphSchemaDesignerHostTests
{
    [Fact]
    public void ConstructorRejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new MetadataDescriptorButterMorphSchemaDesignerHost(
            null!,
            new CapturingMetadataDescriptorApplicationService(),
            new PayloadSchemaDefinitionHydrator(),
            new PayloadSchemaBuilder()));
        Assert.Throws<ArgumentNullException>(() => new MetadataDescriptorButterMorphSchemaDesignerHost(
            new EmptyMetadataDescriptorRepository(),
            null!,
            new PayloadSchemaDefinitionHydrator(),
            new PayloadSchemaBuilder()));
        Assert.Throws<ArgumentNullException>(() => new MetadataDescriptorButterMorphSchemaDesignerHost(
            new EmptyMetadataDescriptorRepository(),
            new CapturingMetadataDescriptorApplicationService(),
            null!,
            new PayloadSchemaBuilder()));
        Assert.Throws<ArgumentNullException>(() => new MetadataDescriptorButterMorphSchemaDesignerHost(
            new EmptyMetadataDescriptorRepository(),
            new CapturingMetadataDescriptorApplicationService(),
            new PayloadSchemaDefinitionHydrator(),
            null!));
    }

    [Fact]
    public async Task SaveCreatesMetadataDescriptorFromButterMorphPayloadSchema()
    {
        var service = new CapturingMetadataDescriptorApplicationService();
        var host = new MetadataDescriptorButterMorphSchemaDesignerHost(
            new EmptyMetadataDescriptorRepository(),
            service,
            new PayloadSchemaDefinitionHydrator(),
            new PayloadSchemaBuilder());

        using var fieldSchema = JsonDocument.Parse("""{"type":"string"}""");
        using var sourceKey = JsonDocument.Parse("\"AuditMetadata\"");
        var result = await host.Save(new ButterMorphPayloadSchemaDesignerSaveRequest
        {
            ContextKey = "metadata:new",
            Definition = new PayloadSchemaDefinition
            {
                Key = "audit_metadata",
                Name = "Audit metadata",
                Description = "Audit fields propagated through orchestration messages.",
                Version = "1.0.0",
                Type = "object",
                Properties = new Dictionary<string, JsonElement>
                {
                    ["trace_id"] = fieldSchema.RootElement.Clone()
                },
                Metadata = new Dictionary<string, JsonElement>
                {
                    ["sourceKey"] = sourceKey.RootElement.Clone()
                }
            }
        });

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(service.LastUpsert);
        var command = service.LastUpsert!;
        Assert.Equal(string.Empty, command.Id);
        Assert.Equal("audit_metadata", command.Key);
        Assert.Equal("AuditMetadata", command.SourceKey);
        Assert.Equal("Audit metadata", command.DisplayName);
        Assert.Contains("\"trace_id\"", command.SchemaJson);
        Assert.Contains("\"type\"", command.SchemaJson);
    }

    [Fact]
    public async Task SaveUsesSourceKeyFallbacksFromNullAndNumericMetadata()
    {
        var service = new CapturingMetadataDescriptorApplicationService();
        var host = new MetadataDescriptorButterMorphSchemaDesignerHost(
            new EmptyMetadataDescriptorRepository(),
            service,
            new PayloadSchemaDefinitionHydrator(),
            new PayloadSchemaBuilder());

        using var nullSourceKey = JsonDocument.Parse("null");
        using var numberSourceKey = JsonDocument.Parse("123");
        using var fieldSchema = JsonDocument.Parse("""{"type":"string"}""");

        var nullResult = await host.Save(new ButterMorphPayloadSchemaDesignerSaveRequest
        {
            ContextKey = "metadata:new",
            Definition = new PayloadSchemaDefinition
            {
                Key = "audit_metadata",
                Name = "",
                Type = "object",
                Properties = new Dictionary<string, JsonElement>
                {
                    ["trace_id"] = fieldSchema.RootElement.Clone()
                },
                Metadata = new Dictionary<string, JsonElement>
                {
                    ["sourceKey"] = nullSourceKey.RootElement.Clone()
                }
            }
        });
        var nullCommand = service.LastUpsert!;

        var numericResult = await host.Save(new ButterMorphPayloadSchemaDesignerSaveRequest
        {
            ContextKey = "metadata:new",
            Definition = new PayloadSchemaDefinition
            {
                Key = "numeric_metadata",
                Name = "",
                Type = "object",
                Properties = new Dictionary<string, JsonElement>
                {
                    ["trace_id"] = fieldSchema.RootElement.Clone()
                },
                Metadata = new Dictionary<string, JsonElement>
                {
                    ["sourceKey"] = numberSourceKey.RootElement.Clone()
                }
            }
        });
        var numericCommand = service.LastUpsert!;

        Assert.True(nullResult.Succeeded, nullResult.Message);
        Assert.Equal("audit_metadata", nullCommand.SourceKey);
        Assert.Equal("audit_metadata", nullCommand.DisplayName);
        Assert.True(numericResult.Succeeded, numericResult.Message);
        Assert.Equal("123", numericCommand.SourceKey);
        Assert.Equal("numeric_metadata", numericCommand.DisplayName);
    }

    [Fact]
    public async Task SaveUpdatesExistingMetadataDescriptorFromButterMorphPayloadSchema()
    {
        var descriptor = new MetadataDescriptor
        {
            Id = Id.New(),
            Key = "audit_metadata",
            SourceKey = "AuditMetadata",
            DisplayName = "Audit metadata",
            Description = "Original schema",
            SchemaJson = """{"type":"object","properties":{}}""",
            ContentHash = "original",
            CreatedOnUtc = DateTime.UtcNow,
        };
        var service = new CapturingMetadataDescriptorApplicationService();
        var host = new MetadataDescriptorButterMorphSchemaDesignerHost(
            new SingleMetadataDescriptorRepository(descriptor),
            service,
            new PayloadSchemaDefinitionHydrator(),
            new PayloadSchemaBuilder());

        using var fieldSchema = JsonDocument.Parse("""{"type":"string","format":"date-time"}""");
        var result = await host.Save(new ButterMorphPayloadSchemaDesignerSaveRequest
        {
            ContextKey = $"metadata:{descriptor.Id}",
            Definition = new PayloadSchemaDefinition
            {
                Key = "audit_metadata",
                Name = "Audit metadata",
                Description = "Updated schema",
                Version = "1.0.0",
                Type = "object",
                Properties = new Dictionary<string, JsonElement>
                {
                    ["created_at"] = fieldSchema.RootElement.Clone()
                }
            }
        });

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(service.LastUpsert);
        Assert.Equal(descriptor.Id.ToString(), service.LastUpsert!.Id);
        Assert.Equal("AuditMetadata", service.LastUpsert.SourceKey);
        Assert.Contains("\"created_at\"", service.LastUpsert.SchemaJson);
    }

    [Fact]
    public async Task LoadExistingDescriptorUsesDescriptorFallbacksAndReportsMissingDescriptors()
    {
        var descriptor = new MetadataDescriptor
        {
            Id = Id.New(),
            Key = "audit_metadata",
            SourceKey = "",
            DisplayName = "Audit metadata",
            Description = null,
            SchemaJson = "",
            ContentHash = "original",
            CreatedOnUtc = DateTime.UtcNow,
        };
        var host = new MetadataDescriptorButterMorphSchemaDesignerHost(
            new SingleMetadataDescriptorRepository(descriptor),
            new CapturingMetadataDescriptorApplicationService(),
            new PayloadSchemaDefinitionHydrator(),
            new PayloadSchemaBuilder());
        var missingHost = new MetadataDescriptorButterMorphSchemaDesignerHost(
            new EmptyMetadataDescriptorRepository(),
            new CapturingMetadataDescriptorApplicationService(),
            new PayloadSchemaDefinitionHydrator(),
            new PayloadSchemaBuilder());

        var loaded = await host.Load(new ButterMorphPayloadSchemaDesignerLoadRequest { ContextKey = $"metadata:{descriptor.Id}" });
        var missing = await missingHost.Load(new ButterMorphPayloadSchemaDesignerLoadRequest { ContextKey = $"metadata:{Id.New()}" });

        Assert.Equal("audit_metadata", loaded.Key);
        Assert.Equal("Audit metadata", loaded.Name);
        Assert.Equal("audit_metadata", loaded.Metadata["sourceKey"]);
        Assert.Contains("additionalProperties", loaded.JsonSchema, StringComparison.Ordinal);
        Assert.Contains("not found", missing.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveRejectsMetadataKeysWithHyphen()
    {
        var host = new MetadataDescriptorButterMorphSchemaDesignerHost(
            new EmptyMetadataDescriptorRepository(),
            new CapturingMetadataDescriptorApplicationService(),
            new PayloadSchemaDefinitionHydrator(),
            new PayloadSchemaBuilder());

        var result = await host.Save(new ButterMorphPayloadSchemaDesignerSaveRequest
        {
            ContextKey = "metadata:new",
            Definition = new PayloadSchemaDefinition
            {
                Key = "audit-metadata",
                Name = "Audit metadata",
                Version = "1.0.0",
                Type = "object",
            }
        });

        Assert.False(result.Succeeded);
        Assert.Contains("underscores", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAndSaveRejectInvalidContextsAndDefinitions()
    {
        var host = new MetadataDescriptorButterMorphSchemaDesignerHost(
            new EmptyMetadataDescriptorRepository(),
            new CapturingMetadataDescriptorApplicationService(),
            new PayloadSchemaDefinitionHydrator(),
            new PayloadSchemaBuilder());

        var nullLoad = await host.Load(null!);
        var emptyContextLoad = await host.Load(new ButterMorphPayloadSchemaDesignerLoadRequest { ContextKey = "metadata:" });
        var invalidIdLoad = await host.Load(new ButterMorphPayloadSchemaDesignerLoadRequest { ContextKey = "metadata:not-a-ulid" });
        var invalidSave = await host.Save(new ButterMorphPayloadSchemaDesignerSaveRequest { ContextKey = "invalid" });
        var nullDefinition = await host.Save(new ButterMorphPayloadSchemaDesignerSaveRequest
        {
            ContextKey = "metadata:new",
            Definition = null!
        });
        var blankKey = await host.Save(new ButterMorphPayloadSchemaDesignerSaveRequest
        {
            ContextKey = "metadata:new",
            Definition = new PayloadSchemaDefinition
            {
                Key = " ",
                Type = "object"
            }
        });
        var missingDescriptor = await host.Save(new ButterMorphPayloadSchemaDesignerSaveRequest
        {
            ContextKey = $"metadata:{Id.New()}",
            Definition = new PayloadSchemaDefinition
            {
                Key = "audit_metadata",
                Type = "object"
            }
        });

        Assert.Contains("invalid", nullLoad.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("invalid", emptyContextLoad.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("invalid", invalidIdLoad.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("invalid", invalidSave.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("did not produce", nullDefinition.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("required", blankKey.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not found", missingDescriptor.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveReportsApplicationServiceFailures()
    {
        var host = new MetadataDescriptorButterMorphSchemaDesignerHost(
            new EmptyMetadataDescriptorRepository(),
            new ThrowingMetadataDescriptorApplicationService(),
            new PayloadSchemaDefinitionHydrator(),
            new PayloadSchemaBuilder());

        var result = await host.Save(new ButterMorphPayloadSchemaDesignerSaveRequest
        {
            ContextKey = "metadata:new",
            Definition = new PayloadSchemaDefinition
            {
                Key = "audit_metadata",
                Name = "Audit metadata",
                Type = "object"
            }
        });

        Assert.False(result.Succeeded);
        Assert.Equal("Metadata key already exists.", result.Message);
    }

    [Fact]
    public async Task LoadAddsTemporalSchemaTypesToMetadataDesigner()
    {
        var host = new MetadataDescriptorButterMorphSchemaDesignerHost(
            new EmptyMetadataDescriptorRepository(),
            new CapturingMetadataDescriptorApplicationService(),
            new PayloadSchemaDefinitionHydrator(),
            new PayloadSchemaBuilder());

        var result = await host.Load(new ButterMorphPayloadSchemaDesignerLoadRequest { ContextKey = "metadata:new" });
        var names = result.SchemaTypes.Select(x => x.Name).ToArray();

        Assert.Contains("DateTime", names);
        Assert.Contains("Date", names);
        Assert.Contains("Time", names);
        Assert.Contains("TimeSpan", names);
        Assert.Contains(result.MetadataDefinition!.Fields, x => x.Key == "sourceKey");
    }

    [Fact]
    public async Task SaveReportsButterMorphBuilderFailuresAndHydratesFallbackIdentity()
    {
        var capturedInput = default(PayloadSchemaDesignInput);
        var hydrator = Substitute.For<IPayloadSchemaDefinitionHydrator>();
        hydrator.Hydrate(Arg.Any<PayloadSchemaDefinition>()).Returns(new PayloadSchemaDesignInput
        {
            Key = " ",
            Name = "",
            Version = null
        });
        var builder = Substitute.For<IPayloadSchemaBuilder>();
        builder
            .Build(
                Arg.Do<PayloadSchemaDesignInput>(input => capturedInput = input),
                Arg.Any<IReadOnlyCollection<SchemaTypeCatalogItem>>(),
                Arg.Any<IReadOnlyCollection<FieldMetadataCatalogItem>>())
            .Returns(new PayloadSchemaDesignResult
            {
                Succeeded = false,
                JsonSchema = "",
                Diagnostics =
                [
                    new DiagnosticEntry
                    {
                        Code = "BM001",
                        Message = "field schema is invalid",
                        Path = "$.properties.field",
                        Severity = "Error"
                    }
                ]
            });
        var host = new MetadataDescriptorButterMorphSchemaDesignerHost(
            new EmptyMetadataDescriptorRepository(),
            new CapturingMetadataDescriptorApplicationService(),
            hydrator,
            builder);

        var result = await host.Save(new ButterMorphPayloadSchemaDesignerSaveRequest
        {
            ContextKey = "metadata:new",
            Definition = new PayloadSchemaDefinition
            {
                Key = "audit_metadata",
                Name = "",
                Type = "object"
            }
        });

        Assert.False(result.Succeeded);
        Assert.Contains("ButterMorph", result.Message, StringComparison.Ordinal);
        Assert.Contains(":", result.Message, StringComparison.Ordinal);
        Assert.NotNull(capturedInput);
        Assert.Equal("audit_metadata", capturedInput.Key);
        Assert.Equal("audit_metadata", capturedInput.Name);
        Assert.Equal("1.0.0", capturedInput.Version);
    }

    private sealed class CapturingMetadataDescriptorApplicationService : IMetadataDescriptorApplicationService
    {
        public UpsertMetadataDescriptorCommand? LastUpsert { get; private set; }

        public Task<string> Upsert(UpsertMetadataDescriptorCommand command, CancellationToken cancellationToken = default)
        {
            LastUpsert = command;
            return Task.FromResult("01K6AW24N95NS6G3W7CNZQK4S2");
        }

        public Task<bool> Delete(DeleteMetadataDescriptorCommand command, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<MetadataDescriptorModel> GetById(GetMetadataDescriptorByIdQuery query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ApplicationPagedResult<MetadataDescriptorModel>> GetAll(GetMetadataDescriptorsQuery query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingMetadataDescriptorApplicationService : IMetadataDescriptorApplicationService
    {
        public Task<string> Upsert(UpsertMetadataDescriptorCommand command, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Metadata key already exists.");

        public Task<bool> Delete(DeleteMetadataDescriptorCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MetadataDescriptorModel> GetById(GetMetadataDescriptorByIdQuery query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ApplicationPagedResult<MetadataDescriptorModel>> GetAll(GetMetadataDescriptorsQuery query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class EmptyMetadataDescriptorRepository : IMetadataDescriptorRepository
    {
        public Task Upsert(MetadataDescriptor descriptor, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Delete(Id descriptorId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<MetadataDescriptor> GetById(Id descriptorId, CancellationToken cancellationToken = default)
            => throw new KeyNotFoundException();

        public Task<MetadataDescriptor> GetByKey(string key, CancellationToken cancellationToken = default)
            => Task.FromResult((MetadataDescriptor)null!);

        public Task<IReadOnlyCollection<MetadataDescriptor>> GetAllDescriptors(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<MetadataDescriptor>>(Array.Empty<MetadataDescriptor>());

        public Task<PagedResult<MetadataDescriptor>> GetAll(
            PagedSettings pagedSettings,
            string searchText = "",
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<MetadataDescriptor>(1, 0, 0, pagedSettings.PageSize, Array.Empty<MetadataDescriptor>()));
    }

    private sealed class SingleMetadataDescriptorRepository : IMetadataDescriptorRepository
    {
        private readonly MetadataDescriptor _descriptor;

        public SingleMetadataDescriptorRepository(MetadataDescriptor descriptor)
        {
            _descriptor = descriptor;
        }

        public Task Upsert(MetadataDescriptor descriptor, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Delete(Id descriptorId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<MetadataDescriptor> GetById(Id descriptorId, CancellationToken cancellationToken = default)
            => Task.FromResult(descriptorId == _descriptor.Id ? _descriptor : throw new KeyNotFoundException());

        public Task<MetadataDescriptor> GetByKey(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Equals(key, _descriptor.Key, StringComparison.Ordinal) ? _descriptor : null!);

        public Task<IReadOnlyCollection<MetadataDescriptor>> GetAllDescriptors(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<MetadataDescriptor>>([_descriptor]);

        public Task<PagedResult<MetadataDescriptor>> GetAll(
            PagedSettings pagedSettings,
            string searchText = "",
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<MetadataDescriptor>(1, 1, 1, pagedSettings.PageSize, [_descriptor]));
    }
}
