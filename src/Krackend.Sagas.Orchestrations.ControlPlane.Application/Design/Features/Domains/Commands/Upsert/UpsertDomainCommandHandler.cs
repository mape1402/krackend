using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles domain upsert command.
/// </summary>
public sealed class UpsertDomainCommandHandler : IRequestHandler<UpsertDomainCommand, string>
{
    private readonly IDomainRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpsertDomainCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Domain repository dependency.</param>
    public UpsertDomainCommandHandler(IDomainRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public async Task<string> Handle(UpsertDomainCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var isCreate = string.IsNullOrWhiteSpace(request.Id);
        var id = isCreate ? Ulid.NewUlid().ToString() : request.Id;
        var current = isCreate
            ? null
            : await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);

        var duplicate = await _repository.GetByKey(request.Key.Trim(), cancellationToken);
        if (duplicate is not null && !string.Equals(duplicate.Id.ToString(), id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Domain key '{request.Key}' already exists.");
        }

        var model = new Domain
        {
            Id = PrimitiveParser.ParseId(id),
            Key = request.Key.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            IsActive = current?.IsActive ?? true,
            CreatedOnUtc = current?.CreatedOnUtc ?? now,
            UpdatedOnUtc = isCreate ? null : now,
        };

        await _repository.Upsert(model, cancellationToken);
        return id;
    }
}
