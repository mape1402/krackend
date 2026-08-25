using Microsoft.AspNetCore.DataProtection;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Protects runtime design node secrets using ASP.NET Core Data Protection.
/// </summary>
public sealed class DataProtectionRuntimeDesignNodeSecretProtector : IRuntimeDesignNodeSecretProtector
{
    private readonly IDataProtector _protector;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataProtectionRuntimeDesignNodeSecretProtector"/> class.
    /// </summary>
    public DataProtectionRuntimeDesignNodeSecretProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _protector = provider.CreateProtector("Krackend.Sagas.Orchestrations.Runtime.DesignNodes.Secrets.v1");
    }

    /// <inheritdoc />
    public string Protect(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("Secret is required.", nameof(secret));
        }

        return _protector.Protect(secret);
    }

    /// <inheritdoc />
    public string Unprotect(string protectedSecret)
    {
        if (string.IsNullOrWhiteSpace(protectedSecret))
        {
            throw new ArgumentException("Protected secret is required.", nameof(protectedSecret));
        }

        return _protector.Unprotect(protectedSecret);
    }
}
