using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Krackend.Sagas.Orchestrations.Security.Api;
using Krackend.Sagas.Orchestrations.Security.AspNetCore;
using Krackend.Sagas.Orchestrations.Security.Authorization;
using Krackend.Sagas.Orchestrations.Security.Configuration;
using Krackend.Sagas.Orchestrations.Security.Core;
using Krackend.Sagas.Orchestrations.Security.DependencyInjection;
using Krackend.Sagas.Orchestrations.Security.Storage;
using Krackend.Sagas.Orchestrations.Security.Storage.EntityFramework;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Tests.Security;

public sealed class KrackendSecurityApiAuthorizationTests
{
    [Fact]
    public async Task PoliciesReturnUnauthorizedForbiddenAndSuccessForExpectedRoles()
    {
        await using var app = await BuildApp();

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/secure/read")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.GetAsync("/secure/read", "unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/secure/read", "bootstrap-admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.PostAsync("/secure/write", "reader")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.PostAsync("/secure/write", "designer")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.PostAsync("/secure/release", "release-manager")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.PostAsync("/secure/release", "reader")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.PostAsync("/secure/runtime", "runtime-operator")).StatusCode);
    }

    [Fact]
    public async Task SecurityAdministrationApiRequiresSecurityManagePermission()
    {
        await using var app = await BuildApp();

        var readerResponse = await app.GetAsync("/api/v1/control-plane/security/roles", "reader");
        var adminResponse = await app.GetAsync("/api/v1/control-plane/security/roles", "security-admin");
        var createResponse = await app.PostAsJsonAsync(
            "/api/v1/control-plane/security/subjects",
            new UpsertKrackendSubjectRequest
            {
                Provider = "test",
                SubjectId = "new-user",
                DisplayName = "New User",
                Email = "new@example.test"
            },
            "security-admin");

        Assert.Equal(HttpStatusCode.Forbidden, readerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
    }

    private static async Task<TestHost> BuildApp()
    {
        var url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        var databaseName = Guid.NewGuid().ToString("N");
        var databaseRoot = new InMemoryDatabaseRoot();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseUrls(url);
        builder.Services
            .AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddKrackendSecurity(options =>
        {
            options.RequireKnownSubject = true;
            options.Subject.Provider = "test";
            options.BootstrapAdmins.Add(new KrackendBootstrapSubject
            {
                Provider = "test",
                SubjectId = "bootstrap-admin"
            });
        });
        builder.Services.AddKrackendSecurityStorageEntityFramework(options => options.UseInMemoryDatabase(databaseName, databaseRoot));

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/secure/read", () => Results.Ok()).RequireAuthorization(KrackendAuthorizationPolicies.ControlPlaneRead);
        app.MapPost("/secure/write", () => Results.Ok()).RequireAuthorization(KrackendAuthorizationPolicies.ControlPlaneDesignWrite);
        app.MapPost("/secure/release", () => Results.Ok()).RequireAuthorization(KrackendAuthorizationPolicies.ControlPlaneReleaseExecute);
        app.MapPost("/secure/runtime", () => Results.Ok()).RequireAuthorization(KrackendAuthorizationPolicies.RuntimeManage);
        app.MapKrackendSecurityAdministrationApi();

        await Seed(app.Services);
        await app.StartAsync();
        return new TestHost(app, url);
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task Seed(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await AddSubject(scope, "reader");
        await AddSubject(scope, "designer");
        await AddSubject(scope, "release-manager");
        await AddSubject(scope, "runtime-operator");
        await AddSubject(scope, "security-admin");

        var roles = scope.ServiceProvider.GetRequiredService<IKrackendRoleAssignmentRepository>();
        await AddRole(roles, "reader", KrackendRoles.Reader, KrackendAuthorizationScopeTypes.Global);
        await AddRole(roles, "designer", KrackendRoles.Designer, KrackendAuthorizationScopeTypes.ControlPlane);
        await AddRole(roles, "release-manager", KrackendRoles.ReleaseManager, KrackendAuthorizationScopeTypes.ControlPlane);
        await AddRole(roles, "runtime-operator", KrackendRoles.RuntimeOperator, KrackendAuthorizationScopeTypes.Runtime);
        await AddRole(roles, "security-admin", KrackendRoles.SecurityAdmin, KrackendAuthorizationScopeTypes.ControlPlane);
    }

    private static async Task AddSubject(IServiceScope scope, string subjectId)
        => await scope.ServiceProvider.GetRequiredService<IKrackendSubjectRepository>().Upsert(new KrackendSubject
        {
            Id = subjectId,
            Provider = "test",
            SubjectId = subjectId,
            DisplayName = subjectId,
            IsEnabled = true
        });

    private static async Task AddRole(IKrackendRoleAssignmentRepository repository, string subjectId, string role, string scopeType)
        => await repository.Upsert(new KrackendRoleAssignment
        {
            Id = $"{subjectId}-{role}",
            Provider = "test",
            SubjectId = subjectId,
            Role = role,
            ScopeType = scopeType,
            ScopeId = string.Empty,
            IsEnabled = true
        });

    private sealed class TestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        public TestHost(WebApplication app, string url)
        {
            _app = app;
            Client = new HttpClient { BaseAddress = new Uri(url) };
        }

        public HttpClient Client { get; }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.DisposeAsync();
        }

        public Task<HttpResponseMessage> GetAsync(string path, string subjectId = "")
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            AddSubject(request, subjectId);
            return Client.SendAsync(request);
        }

        public Task<HttpResponseMessage> PostAsync(string path, string subjectId = "")
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { }) };
            AddSubject(request, subjectId);
            return Client.SendAsync(request);
        }

        public Task<HttpResponseMessage> PostAsJsonAsync<T>(string path, T body, string subjectId)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            AddSubject(request, subjectId);
            return Client.SendAsync(request);
        }

        private static void AddSubject(HttpRequestMessage request, string subjectId)
        {
            if (!string.IsNullOrWhiteSpace(subjectId))
            {
                request.Headers.Add("x-test-subject", subjectId);
            }
        }
    }

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var subject = Request.Headers["x-test-subject"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(subject))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
            [
                new Claim("oid", subject),
                new Claim(ClaimTypes.Name, subject)
            ], Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
