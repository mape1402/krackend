using System.Security.Claims;
using Krackend.Security.Core;

namespace Krackend.Security.Subjects;

/// <summary>
/// Resolves the product external subject from an authenticated host principal.
/// </summary>
public interface IKrackendSubjectResolver
{
    /// <summary>
    /// Resolves the external subject from the supplied principal.
    /// </summary>
    /// <param name="user">Authenticated host principal.</param>
    /// <returns>Resolved external subject.</returns>
    KrackendExternalSubject Resolve(ClaimsPrincipal user);
}
