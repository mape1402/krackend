namespace Krackend.Sagas.Orchestrations.Runtime.Api;

/// <summary>
/// Represents a credential generation request.
/// </summary>
public sealed record CredentialIssuerRequest(string IssuerBaseUrl);
