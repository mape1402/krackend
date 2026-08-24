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
}

