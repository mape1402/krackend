using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles metadata descriptor upsert command.
/// </summary>
public sealed class UpsertMetadataDescriptorCommandHandler : IRequestHandler<UpsertMetadataDescriptorCommand, string>
{
    private readonly IMetadataDescriptorRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpsertMetadataDescriptorCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Metadata descriptor repository dependency.</param>
    public UpsertMetadataDescriptorCommandHandler(IMetadataDescriptorRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public async Task<string> Handle(UpsertMetadataDescriptorCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var isCreate = string.IsNullOrWhiteSpace(request.Id);
        var id = isCreate ? Ulid.NewUlid().ToString() : request.Id;
        var current = isCreate
            ? null
            : await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);
        var key = request.Key.Trim();
        var sourceKey = string.IsNullOrWhiteSpace(request.SourceKey)
            ? key
            : request.SourceKey.Trim();

        var duplicate = await _repository.GetByKey(key, cancellationToken);
        if (duplicate is not null && !string.Equals(duplicate.Id.ToString(), id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Metadata descriptor key '{request.Key}' already exists.");
        }

        var duplicateSourceKey = (await _repository.GetAllDescriptors(cancellationToken))
            .FirstOrDefault(descriptor =>
                !string.Equals(descriptor.Id.ToString(), id, StringComparison.Ordinal) &&
                string.Equals(descriptor.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase));
        if (duplicateSourceKey is not null)
        {
            throw new InvalidOperationException($"Metadata source key '{sourceKey}' is already used by descriptor '{duplicateSourceKey.Key}'.");
        }

        var normalizedSchema = MetadataDescriptorSchemaHasher.Normalize(request.SchemaJson);
        var model = new MetadataDescriptor
        {
            Id = PrimitiveParser.ParseId(id),
            Key = key,
            SourceKey = sourceKey,
            DisplayName = request.DisplayName.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            SchemaJson = normalizedSchema,
            ContentHash = MetadataDescriptorSchemaHasher.ComputeHash(normalizedSchema),
            CreatedOnUtc = current?.CreatedOnUtc ?? now,
            UpdatedOnUtc = isCreate ? null : now,
        };

        await _repository.Upsert(model, cancellationToken);
        return id;
    }
}
