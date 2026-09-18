using System.Security.Claims;
using Krackend.Sagas.Orchestrations.Security.Configuration;
using Krackend.Sagas.Orchestrations.Security.Core;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Security.Subjects;

/// <summary>
/// Resolves external subjects from configurable claim types.
/// </summary>
public sealed class DefaultKrackendSubjectResolver : IKrackendSubjectResolver
{
    private readonly IOptions<KrackendSecurityOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultKrackendSubjectResolver"/> class.
    /// </summary>
    /// <param name="options">Security options.</param>
    public DefaultKrackendSubjectResolver(IOptions<KrackendSecurityOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public KrackendExternalSubject Resolve(ClaimsPrincipal user)
    {
        if (user is null || user.Identity?.IsAuthenticated != true)
        {
            return new KrackendExternalSubject();
        }

        var subjectOptions = _options.Value.Subject;
        var subjectId = FindFirstValue(user, subjectOptions.SubjectIdClaimTypes);
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return new KrackendExternalSubject
            {
                Provider = subjectOptions.Provider,
            };
        }

        return new KrackendExternalSubject
        {
            Provider = subjectOptions.Provider,
            SubjectId = subjectId,
            Email = FindFirstValue(user, subjectOptions.EmailClaimTypes),
            DisplayName = FindFirstValue(user, subjectOptions.DisplayNameClaimTypes),
            GroupIds = FindValues(user, subjectOptions.GroupClaimTypes),
        };
    }

    private static string FindFirstValue(ClaimsPrincipal user, IEnumerable<string> claimTypes)
        => claimTypes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(type => user.FindFirst(type)?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static IReadOnlyCollection<string> FindValues(ClaimsPrincipal user, IEnumerable<string> claimTypes)
        => claimTypes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .SelectMany(type => user.FindAll(type))
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
