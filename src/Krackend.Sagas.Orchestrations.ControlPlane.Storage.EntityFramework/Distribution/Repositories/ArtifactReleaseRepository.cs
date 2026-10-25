using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Sieve.Models;
using Sieve.Services;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Repositories;

public sealed class ArtifactRepository : IArtifactRepository
{
    private readonly ControlPlaneDbContext _dbContext;
    private readonly ISieveProcessor _sieveProcessor;

    public ArtifactRepository(ControlPlaneDbContext dbContext, ISieveProcessor sieveProcessor)
    {
        _dbContext = dbContext;
        _sieveProcessor = sieveProcessor;
    }

    public async Task Create(Artifact artifact, CancellationToken cancellationToken = default)
    {
        _dbContext.Artifacts.Add(new ArtifactEntity
        {
            Id = artifact.Id,
            OrchestrationDefinitionId = artifact.OrchestrationDefinitionId,
            OrchestrationVersionId = artifact.OrchestrationVersionId,
            OrchestrationDisplayName = artifact.OrchestrationDisplayName,
            VersionLabel = artifact.VersionLabel,
            VersionNumber = artifact.VersionNumber,
            ArtifactType = artifact.ArtifactType,
            SchemaVersion = artifact.SchemaVersion,
            Payload = artifact.Payload,
            Metadata = artifact.Metadata,
            SourceEvent = artifact.SourceEvent,
            SourceVersion = artifact.SourceVersion,
            Checksum = artifact.Checksum,
            IsPublished = artifact.IsPublished,
            CreatedAtUtc = artifact.CreatedAtUtc,
            PublishedAtUtc = artifact.PublishedAtUtc
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetPublished(Id artifactId, bool isPublished, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Artifacts.FirstAsync(x => x.Id == artifactId, cancellationToken);
        entity.IsPublished = isPublished;
        entity.PublishedAtUtc = isPublished ? DateTime.UtcNow : null;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Artifact> GetById(Id artifactId, CancellationToken cancellationToken = default)
    {
        var x = await _dbContext.Artifacts.AsNoTracking().FirstAsync(y => y.Id == artifactId, cancellationToken);
        return new Artifact
        {
            Id = x.Id,
            OrchestrationDefinitionId = x.OrchestrationDefinitionId,
            OrchestrationVersionId = x.OrchestrationVersionId,
            OrchestrationDisplayName = x.OrchestrationDisplayName,
            VersionLabel = x.VersionLabel,
            VersionNumber = x.VersionNumber,
            ArtifactType = x.ArtifactType,
            SchemaVersion = x.SchemaVersion,
            Payload = x.Payload,
            Metadata = x.Metadata,
            SourceEvent = x.SourceEvent,
            SourceVersion = x.SourceVersion,
            Checksum = x.Checksum,
            IsPublished = x.IsPublished,
            CreatedAtUtc = x.CreatedAtUtc,
            PublishedAtUtc = x.PublishedAtUtc
        };
    }

    public async Task<Artifact> GetLatestForOrchestrationVersion(
        Id orchestrationVersionId,
        string artifactType,
        CancellationToken cancellationToken = default)
    {
        var x = await _dbContext.Artifacts.AsNoTracking()
            .Where(y => y.OrchestrationVersionId == orchestrationVersionId.ToString()
                && y.ArtifactType == artifactType)
            .OrderByDescending(y => y.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return x is null
            ? null
            : new Artifact
            {
                Id = x.Id,
                OrchestrationDefinitionId = x.OrchestrationDefinitionId,
                OrchestrationVersionId = x.OrchestrationVersionId,
                OrchestrationDisplayName = x.OrchestrationDisplayName,
                VersionLabel = x.VersionLabel,
                VersionNumber = x.VersionNumber,
                ArtifactType = x.ArtifactType,
                SchemaVersion = x.SchemaVersion,
                Payload = x.Payload,
                Metadata = x.Metadata,
                SourceEvent = x.SourceEvent,
                SourceVersion = x.SourceVersion,
                Checksum = x.Checksum,
                IsPublished = x.IsPublished,
                CreatedAtUtc = x.CreatedAtUtc,
                PublishedAtUtc = x.PublishedAtUtc
            };
    }

    public async Task<PagedResult<Artifact>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Artifacts.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc);
        var processed = _sieveProcessor.Apply(new SieveModel { Page = pagedSettings.PageNumber, PageSize = pagedSettings.PageSize }, query);
        var rows = await processed.ToArrayAsync(cancellationToken);
        var totalRows = await query.CountAsync(cancellationToken);
        var totalPages = totalRows == 0 ? 1 : (int)Math.Ceiling(totalRows / (double)pagedSettings.PageSize);
        return new PagedResult<Artifact>(
            pagedSettings.PageNumber,
            totalPages,
            totalRows,
            pagedSettings.PageSize,
            rows.Select(x => new Artifact
            {
                Id = x.Id,
                OrchestrationDefinitionId = x.OrchestrationDefinitionId,
                OrchestrationVersionId = x.OrchestrationVersionId,
                OrchestrationDisplayName = x.OrchestrationDisplayName,
                VersionLabel = x.VersionLabel,
                VersionNumber = x.VersionNumber,
                ArtifactType = x.ArtifactType,
                SchemaVersion = x.SchemaVersion,
                Payload = x.Payload,
                Metadata = x.Metadata,
                SourceEvent = x.SourceEvent,
                SourceVersion = x.SourceVersion,
                Checksum = x.Checksum,
                IsPublished = x.IsPublished,
                CreatedAtUtc = x.CreatedAtUtc,
                PublishedAtUtc = x.PublishedAtUtc
            }).ToArray());
    }

    /// <inheritdoc />
    public async Task<PagedResult<Artifact>> Search(PagedSettings settings, string orchestrationId, string search, string sort, bool descending, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, settings?.PageNumber ?? 1);
        var size = Math.Clamp(settings?.PageSize ?? 25, 1, 100);
        var query = _dbContext.Artifacts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(orchestrationId)) query = query.Where(x => x.OrchestrationDefinitionId == orchestrationId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.OrchestrationDisplayName.Contains(term) || x.VersionNumber.Contains(term) ||
                x.VersionLabel.Contains(term) || x.ArtifactType.Contains(term) || x.Checksum.Contains(term));
        }
        var total = await query.LongCountAsync(cancellationToken);
        var ordered = (sort, descending) switch
        {
            ("displayName", false) => query.OrderBy(x => x.OrchestrationDisplayName),
            ("displayName", true) => query.OrderByDescending(x => x.OrchestrationDisplayName),
            ("version", false) => query.OrderBy(x => x.VersionNumber),
            ("version", true) => query.OrderByDescending(x => x.VersionNumber),
            ("artifactType", false) => query.OrderBy(x => x.ArtifactType),
            ("artifactType", true) => query.OrderByDescending(x => x.ArtifactType),
            ("created", false) => query.OrderBy(x => x.CreatedAtUtc),
            _ => query.OrderByDescending(x => x.CreatedAtUtc)
        };
        var rows = await ordered.ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new { x.Id, x.OrchestrationDefinitionId, x.OrchestrationVersionId, x.OrchestrationDisplayName,
                x.VersionLabel, x.VersionNumber, x.ArtifactType, x.Checksum, x.IsPublished, x.CreatedAtUtc }).ToArrayAsync(cancellationToken);
        return new PagedResult<Artifact>(page, total == 0 ? 1 : (int)Math.Ceiling(total / (double)size), total, size,
            rows.Select(x => new Artifact { Id = x.Id, OrchestrationDefinitionId = x.OrchestrationDefinitionId,
                OrchestrationVersionId = x.OrchestrationVersionId, OrchestrationDisplayName = x.OrchestrationDisplayName,
                VersionLabel = x.VersionLabel, VersionNumber = x.VersionNumber, ArtifactType = x.ArtifactType,
                Checksum = x.Checksum, IsPublished = x.IsPublished, CreatedAtUtc = x.CreatedAtUtc }).ToArray());
    }
}

