namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

using System.Security.Cryptography;
using System.Text;

/// <summary>Calculates a portable revision for an orchestration designer document.</summary>
public static class OrchestrationDesignerRevision
{
    /// <summary>Calculates the revision of a persisted DSL document.</summary>
    public static string FromDsl(string dsl)
    {
        var value = string.IsNullOrWhiteSpace(dsl) ? string.Empty : dsl;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
