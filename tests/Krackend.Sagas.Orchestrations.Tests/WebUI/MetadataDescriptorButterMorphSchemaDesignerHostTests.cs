using System.Text.Json;
using global::ButterMorph.SchemaDesign;
using global::ButterMorph.Web.Razor;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class MetadataDescriptorButterMorphSchemaDesignerHostTests
{
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
