using Krackend.Sagas.Orchestrations.ControlPlane.Security.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

/// <summary>
/// Handles upsert team command requests.
/// </summary>
public sealed class UpsertTeamCommandHandler : IRequestHandler<UpsertTeamCommand, string>
{
    private readonly ITeamRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpsertTeamCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Team repository dependency.</param>
    public UpsertTeamCommandHandler(ITeamRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public async Task<string> Handle(UpsertTeamCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var isCreate = string.IsNullOrWhiteSpace(request.TeamId);
        var idText = isCreate ? Ulid.NewUlid().ToString() : request.TeamId;
        var current = isCreate ? null : await _repository.GetById(PrimitiveParser.ParseId(idText), cancellationToken);

        var duplicate = await _repository.GetByKey(request.Key.Trim(), cancellationToken);
        if (duplicate is not null && !string.Equals(duplicate.Id.ToString(), idText, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Team key '{request.Key}' already exists.");
        }

        var team = new Team
        {
            Id = PrimitiveParser.ParseId(idText),
            Key = request.Key.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            IsActive = current?.IsActive ?? true,
            CreatedOnUtc = current?.CreatedOnUtc ?? now,
            UpdatedOnUtc = isCreate ? null : now,
        };

        await _repository.Upsert(team, cancellationToken);

        return idText;
    }
}
