namespace Krackend.Sagas.Orchestrations.Tests.Security;

using System.Security.Claims;
using Krackend.Sagas.Orchestrations.Security.Configuration;
using Krackend.Sagas.Orchestrations.Security.Subjects;
using Microsoft.Extensions.Options;

public sealed class KrackendSubjectResolverTests
{
    [Fact]
    public void ResolveReturnsEmptySubjectForNullOrUnauthenticatedUsers()
    {
        var resolver = CreateResolver();

        var missing = resolver.Resolve(null!);
        var anonymous = resolver.Resolve(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.False(missing.IsResolved);
        Assert.False(anonymous.IsResolved);
        Assert.Throws<ArgumentNullException>(() => new DefaultKrackendSubjectResolver(null!));
    }

    [Fact]
    public void ResolveReturnsProviderOnlyWhenAuthenticatedUserHasNoSubjectId()
    {
        var resolver = CreateResolver(options => options.Subject.Provider = "entra");
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Email, "alice@example.test")],
            authenticationType: "test"));

        var subject = resolver.Resolve(user);

        Assert.False(subject.IsResolved);
        Assert.Equal("entra", subject.Provider);
        Assert.Equal(string.Empty, subject.SubjectId);
    }

    [Fact]
    public void ResolveReadsConfiguredClaimsAndDeduplicatesGroups()
    {
        var resolver = CreateResolver(options =>
        {
            options.Subject.Provider = "entra";
            options.Subject.SubjectIdClaimTypes.Insert(0, "custom-subject");
            options.Subject.EmailClaimTypes.Insert(0, "custom-email");
            options.Subject.DisplayNameClaimTypes.Insert(0, "custom-name");
            options.Subject.GroupClaimTypes.Insert(0, "custom-group");
        });
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("custom-subject", " user-1 "),
                new Claim("custom-email", "alice@example.test"),
                new Claim("custom-name", "Alice Example"),
                new Claim("custom-group", "operators"),
                new Claim("custom-group", "Operators"),
                new Claim("custom-group", " "),
            ],
            authenticationType: "test"));

        var subject = resolver.Resolve(user);

        Assert.True(subject.IsResolved);
        Assert.Equal("entra", subject.Provider);
        Assert.Equal(" user-1 ", subject.SubjectId);
        Assert.Equal("alice@example.test", subject.Email);
        Assert.Equal("Alice Example", subject.DisplayName);
        Assert.Equal(["operators"], subject.GroupIds);
    }

    private static DefaultKrackendSubjectResolver CreateResolver(Action<KrackendSecurityOptions>? configure = null)
    {
        var options = new KrackendSecurityOptions();
        configure?.Invoke(options);
        return new DefaultKrackendSubjectResolver(Options.Create(options));
    }
}
