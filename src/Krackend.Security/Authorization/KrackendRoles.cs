namespace Krackend.Security.Authorization;

/// <summary>
/// Defines stable Krackend product role names.
/// </summary>
public static class KrackendRoles
{
    /// <summary>
    /// Read-only product user.
    /// </summary>
    public const string Reader = "Krackend.Reader";

    /// <summary>
    /// User allowed to design orchestration definitions.
    /// </summary>
    public const string Designer = "Krackend.Designer";

    /// <summary>
    /// User allowed to create and execute releases.
    /// </summary>
    public const string ReleaseManager = "Krackend.ReleaseManager";

    /// <summary>
    /// User allowed to operate runtime nodes.
    /// </summary>
    public const string RuntimeOperator = "Krackend.RuntimeOperator";

    /// <summary>
    /// User allowed to administer product security.
    /// </summary>
    public const string SecurityAdmin = "Krackend.SecurityAdmin";

    /// <summary>
    /// User allowed to perform every product operation.
    /// </summary>
    public const string Admin = "Krackend.Admin";

    /// <summary>
    /// Gets all stable role names.
    /// </summary>
    public static IReadOnlyCollection<string> All { get; } =
    [
        Reader,
        Designer,
        ReleaseManager,
        RuntimeOperator,
        SecurityAdmin,
        Admin,
    ];
}
