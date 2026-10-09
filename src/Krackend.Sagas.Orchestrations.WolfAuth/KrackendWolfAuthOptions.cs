namespace Krackend.Sagas.Orchestrations.WolfAuth;

/// <summary>
/// Configures the Krackend login shell that delegates authentication to the host's WolfAuth-backed authentication setup.
/// </summary>
public sealed class KrackendWolfAuthOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether Krackend should require WolfAuth-backed authentication.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the anonymous login page path.
    /// </summary>
    public string LoginPath { get; set; } = "/auth/login";

    /// <summary>
    /// Gets or sets the endpoint that issues the ASP.NET Core authentication challenge.
    /// </summary>
    public string ChallengePath { get; set; } = "/auth/login/challenge";

    /// <summary>
    /// Gets or sets the optional sign-out endpoint path.
    /// </summary>
    public string LogoutPath { get; set; } = "/auth/logout";

    /// <summary>
    /// Gets or sets the query-string parameter used for the post-login return URL.
    /// </summary>
    public string ReturnUrlParameter { get; set; } = "returnUrl";

    /// <summary>
    /// Gets or sets the product name shown on the login page.
    /// </summary>
    public string ApplicationName { get; set; } = "Krackend";

    /// <summary>
    /// Gets or sets the login page subtitle.
    /// </summary>
    public string Subtitle { get; set; } = "Sign in to continue.";

    /// <summary>
    /// Gets or sets the login button text.
    /// </summary>
    public string LoginButtonText { get; set; } = "Login";

    /// <summary>
    /// Gets authentication schemes used by the login challenge. Empty means ASP.NET Core uses the configured default challenge scheme.
    /// </summary>
    public IList<string> ChallengeSchemes { get; } = [];

    /// <summary>
    /// Gets authentication schemes used by sign-out. Empty means ASP.NET Core uses the configured default sign-out scheme.
    /// </summary>
    public IList<string> SignOutSchemes { get; } = [];
}
