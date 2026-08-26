namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Represents the result of validating a bearer token.
/// </summary>
public sealed class ConnectionTokenValidationResult
{
    private ConnectionTokenValidationResult(bool succeeded, string message, ConnectionTokenPrincipal principal)
    {
        Succeeded = succeeded;
        Message = message ?? string.Empty;
        Principal = principal;
    }

    /// <summary>
    /// Gets a value indicating whether validation succeeded.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets a validation message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the authenticated principal when validation succeeds.
    /// </summary>
    public ConnectionTokenPrincipal Principal { get; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="principal">Authenticated principal.</param>
    /// <returns>Successful validation result.</returns>
    public static ConnectionTokenValidationResult Success(ConnectionTokenPrincipal principal)
        => new(true, string.Empty, principal ?? throw new ArgumentNullException(nameof(principal)));

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="message">Failure message.</param>
    /// <returns>Failed validation result.</returns>
    public static ConnectionTokenValidationResult Failure(string message)
        => new(false, message, null);
}
