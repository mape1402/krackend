namespace Krackend.Sagas.Orchestrations.WebUI.Shell.Authentication;

/// <summary>Host-owned configuration for the Zoorce sign-in presentation.</summary>
public sealed class OrchestratorSignInOptions
{
    /// <summary>Gets or sets whether an authentication challenge is available.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the public sign-in page path.</summary>
    public string SignInPath { get; set; } = "/auth/login";

    /// <summary>Gets or sets the host-owned authentication challenge path.</summary>
    public string ChallengePath { get; set; } = "/auth/login/challenge";

    /// <summary>Gets or sets the return URL query string key.</summary>
    public string ReturnUrlParameter { get; set; } = "returnUrl";

    /// <summary>Gets or sets the sign-in heading.</summary>
    public string Title { get; set; } = "Welcome back.";

    /// <summary>Gets or sets the supporting sign-in message.</summary>
    public string Description { get; set; } = "Sign in to continue.";

    /// <summary>Gets or sets the sign-in command label.</summary>
    public string SignInText { get; set; } = "Sign in";
}
