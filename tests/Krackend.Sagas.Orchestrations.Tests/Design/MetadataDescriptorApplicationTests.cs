using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using NSubstitute;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Tests.Design;

public sealed class MetadataDescriptorApplicationTests
{
    [Fact]
    public async Task UpsertHandlerCreatesDescriptorWithNormalizedDefaults()
    {
        var repository = Substitute.For<IMetadataDescriptorRepository>();
        MetadataDescriptor? saved = null;
        repository.GetByKey("customer", Arg.Any<CancellationToken>()).Returns((MetadataDescriptor)null!);
        repository.GetAllDescriptors(Arg.Any<CancellationToken>()).Returns([]);
        repository.Upsert(Arg.Do<MetadataDescriptor>(descriptor => saved = descriptor), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var handler = new UpsertMetadataDescriptorCommandHandler(repository);

        var id = await handler.Handle(new UpsertMetadataDescriptorCommand(
            string.Empty,
            " customer ",
            " ",
            " Customer ",
            " Primary customer metadata ",
            """{ "type": "object", "properties": { "id": { "type": "string" } } }"""), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.NotNull(saved);
        Assert.Equal(id, saved.Id.ToString());
        Assert.Equal("customer", saved.Key);
        Assert.Equal("customer", saved.SourceKey);
        Assert.Equal("Customer", saved.DisplayName);
        Assert.Equal("Primary customer metadata", saved.Description);
        Assert.StartsWith("{", saved.SchemaJson, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(saved.ContentHash));
        Assert.Null(saved.UpdatedOnUtc);
    }

    [Fact]
    public async Task UpsertHandlerUpdatesExistingDescriptorAndPreservesCreatedDate()
    {
        var repository = Substitute.For<IMetadataDescriptorRepository>();
        var id = Id.New();
        var createdOn = DateTime.UtcNow.AddDays(-7);
        var existing = CreateDescriptor(id, "customer", "customer", createdOn);
        MetadataDescriptor? saved = null;
        repository.GetById(id, Arg.Any<CancellationToken>()).Returns(existing);
        repository.GetByKey("customer", Arg.Any<CancellationToken>()).Returns(existing);
        repository.GetAllDescriptors(Arg.Any<CancellationToken>()).Returns([existing]);
        repository.Upsert(Arg.Do<MetadataDescriptor>(descriptor => saved = descriptor), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var handler = new UpsertMetadataDescriptorCommandHandler(repository);

        var result = await handler.Handle(new UpsertMetadataDescriptorCommand(
            id.ToString(),
            " customer ",
            " customer-message ",
            " Customer Updated ",
            null!,
            """{"type":"object","required":["id"]}"""), CancellationToken.None);

        Assert.Equal(id.ToString(), result);
        Assert.NotNull(saved);
        Assert.Equal(id, saved.Id);
        Assert.Equal("customer-message", saved.SourceKey);
        Assert.Equal("Customer Updated", saved.DisplayName);
        Assert.Equal(string.Empty, saved.Description);
        Assert.Equal(createdOn, saved.CreatedOnUtc);
        Assert.NotNull(saved.UpdatedOnUtc);
    }

    [Fact]
    public async Task UpsertHandlerRejectsDuplicateKeyAndSourceKey()
    {
        var repository = Substitute.For<IMetadataDescriptorRepository>();
        var existing = CreateDescriptor(Id.New(), "customer", "customer");
        repository.GetByKey("customer", Arg.Any<CancellationToken>()).Returns(existing);
        var handler = new UpsertMetadataDescriptorCommandHandler(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new UpsertMetadataDescriptorCommand(
            string.Empty,
            "customer",
            string.Empty,
            "Customer",
            string.Empty,
            "{}"), CancellationToken.None));

        repository.ClearReceivedCalls();
        repository.GetByKey("order", Arg.Any<CancellationToken>()).Returns((MetadataDescriptor)null!);
        repository.GetAllDescriptors(Arg.Any<CancellationToken>())
            .Returns([CreateDescriptor(Id.New(), "customer", "external-message")]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new UpsertMetadataDescriptorCommand(
            string.Empty,
            "order",
            " external-message ",
            "Order",
            string.Empty,
            "{}"), CancellationToken.None));
    }

    [Fact]
    public void MapperProjectsDescriptorRowsAndPagedResults()
    {
        var mapper = new MetadataDescriptorApplicationMapper();
        var descriptor = CreateDescriptor(Id.New(), "customer", "customer-message");
        descriptor.Description = null!;
        var model = mapper.ToModel(descriptor);
        var paged = mapper.ToPagedModel(new PagedResult<MetadataDescriptor>(2, 4, 9, 3, [descriptor]));

        Assert.Equal(descriptor.Id.ToString(), model.Id);
        Assert.Equal("customer", model.Key);
        Assert.Equal("customer-message", model.SourceKey);
        Assert.Equal(string.Empty, model.Description);
        Assert.Equal(2, paged.PageNumber);
        Assert.Equal(4, paged.TotalPages);
        Assert.Equal(9, paged.TotalRows);
        Assert.Equal(3, paged.PageSize);
        Assert.Single(paged.Rows);
    }

    [Fact]
    public async Task ApplicationServiceDelegatesOperationsToMediator()
    {
        var model = new MetadataDescriptorModel { Id = "metadata-1", Key = "customer" };
        var paged = new ApplicationPagedResult<MetadataDescriptorModel>(1, 1, 1, 10, [model]);
        var mediator = new RecordingMediator()
            .With("metadata-1")
            .With(true)
            .With(model)
            .With(paged);
        var service = new MetadataDescriptorApplicationService(mediator);
        var upsert = new UpsertMetadataDescriptorCommand(string.Empty, "customer", string.Empty, "Customer", string.Empty, "{}");
        var delete = new DeleteMetadataDescriptorCommand("metadata-1");
        var getById = new GetMetadataDescriptorByIdQuery("metadata-1");
        var getAll = new GetMetadataDescriptorsQuery(new ApplicationPagedSettings { PageNumber = 1, PageSize = 10 }, "customer");

        Assert.Equal("metadata-1", await service.Upsert(upsert));
        Assert.True(await service.Delete(delete));
        Assert.Same(model, await service.GetById(getById));
        Assert.Same(paged, await service.GetAll(getAll));
        Assert.Equal([upsert, delete, getById, getAll], mediator.Requests);
    }

    [Fact]
    public void SchemaHasherNormalizesHashesAndValidatesJsonObjects()
    {
        var schema = """{ "type": "object" }""";
        var normalized = MetadataDescriptorSchemaHasher.Normalize(schema);
        var hash = MetadataDescriptorSchemaHasher.ComputeHash(schema);

        Assert.Equal(schema, normalized);
        Assert.Equal(hash, MetadataDescriptorSchemaHasher.ComputeHash(normalized));
        Assert.True(MetadataDescriptorSchemaHasher.IsJsonObject(schema));
        Assert.False(MetadataDescriptorSchemaHasher.IsJsonObject(""));
        Assert.False(MetadataDescriptorSchemaHasher.IsJsonObject("not-json"));
        Assert.False(MetadataDescriptorSchemaHasher.IsJsonObject("[]"));
        Assert.Throws<InvalidOperationException>(() => MetadataDescriptorSchemaHasher.Normalize("[]"));
    }

    private static MetadataDescriptor CreateDescriptor(
        Id id,
        string key,
        string sourceKey,
        DateTime? createdOn = null)
        => new()
        {
            Id = id,
            Key = key,
            SourceKey = sourceKey,
            DisplayName = key,
            Description = "Description",
            SchemaJson = "{}",
            ContentHash = "hash",
            CreatedOnUtc = createdOn ?? DateTime.UtcNow.AddDays(-1)
        };

    private sealed class RecordingMediator : IMediator
    {
        private readonly Dictionary<Type, object> _responses = [];

        public List<object> Requests { get; } = [];

        public RecordingMediator With<T>(T response)
        {
            _responses[typeof(T)] = response!;
            return this;
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            Requests.Add(request!);
            return Task.CompletedTask;
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult((TResponse)_responses[typeof(TResponse)]);
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }
}
