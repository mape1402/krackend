using Krackend.Sagas.Orchestrations.Security.Core;
using Krackend.Sagas.Orchestrations.Security.Storage;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Security.Storage.EntityFramework.Repositories;

/// <summary>
/// Entity Framework repository for product subjects.
/// </summary>
public sealed class EntityFrameworkKrackendSubjectRepository : IKrackendSubjectRepository
{
    private readonly KrackendSecurityDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkKrackendSubjectRepository"/> class.
    /// </summary>
    /// <param name="dbContext">Security database context.</param>
    public EntityFrameworkKrackendSubjectRepository(KrackendSecurityDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task Upsert(KrackendSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        subject.Provider = Normalize(subject.Provider);
        subject.SubjectId = Normalize(subject.SubjectId);

        var current = await _dbContext.Subjects.FirstOrDefaultAsync(x => x.Id == subject.Id, cancellationToken)
            ?? await _dbContext.Subjects.FirstOrDefaultAsync(x => x.Provider == subject.Provider && x.SubjectId == subject.SubjectId, cancellationToken);

        if (current is null)
        {
            if (string.IsNullOrWhiteSpace(subject.Id))
            {
                subject.Id = NewId();
            }

            subject.CreatedAtUtc = subject.CreatedAtUtc == default ? DateTime.UtcNow : subject.CreatedAtUtc;
            subject.UpdatedAtUtc = DateTime.UtcNow;
            _dbContext.Subjects.Add(subject);
        }
        else
        {
            current.Provider = subject.Provider;
            current.SubjectId = subject.SubjectId;
            current.DisplayName = subject.DisplayName ?? string.Empty;
            current.Email = subject.Email ?? string.Empty;
            current.IsEnabled = subject.IsEnabled;
            current.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetEnabled(string subjectId, bool isEnabled, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.Subjects.FirstOrDefaultAsync(x => x.Id == subjectId, cancellationToken)
            ?? throw new KeyNotFoundException($"Subject '{subjectId}' was not found.");

        current.IsEnabled = isEnabled;
        current.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<KrackendSubject> GetById(string subjectId, CancellationToken cancellationToken = default)
        => _dbContext.Subjects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == subjectId, cancellationToken);

    /// <inheritdoc />
    public Task<KrackendSubject> GetByExternalSubject(string provider, string subjectId, CancellationToken cancellationToken = default)
    {
        var normalizedProvider = Normalize(provider);
        var normalizedSubjectId = Normalize(subjectId);
        return _dbContext.Subjects.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Provider == normalizedProvider && x.SubjectId == normalizedSubjectId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<KrackendPagedResult<KrackendSubject>> GetAll(int pageNumber, int pageSize, string searchText = "", CancellationToken cancellationToken = default)
    {
        var safePageNumber = pageNumber <= 0 ? 1 : pageNumber;
        var safePageSize = pageSize <= 0 ? 25 : pageSize;
        var query = _dbContext.Subjects.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim();
            query = query.Where(x =>
                EF.Functions.Like(x.SubjectId, $"%{term}%") ||
                EF.Functions.Like(x.DisplayName, $"%{term}%") ||
                EF.Functions.Like(x.Email, $"%{term}%"));
        }

        var totalRows = await query.LongCountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling((double)totalRows / safePageSize);
        var rows = await query
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.SubjectId)
            .Skip((safePageNumber - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return new KrackendPagedResult<KrackendSubject>(safePageNumber, totalPages, totalRows, safePageSize, rows);
    }

    private static string Normalize(string value)
        => value?.Trim() ?? string.Empty;

    private static string NewId()
        => Guid.NewGuid().ToString("N");
}
