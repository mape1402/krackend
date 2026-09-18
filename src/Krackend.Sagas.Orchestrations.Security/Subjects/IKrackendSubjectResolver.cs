using System.Security.Claims;
using Krackend.Sagas.Orchestrations.Security.Core;

namespace Krackend.Sagas.Orchestrations.Security.Subjects;

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
