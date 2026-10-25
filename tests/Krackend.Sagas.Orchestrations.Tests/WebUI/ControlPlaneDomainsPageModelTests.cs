using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DomainsIndexModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.Domains.IndexModel;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ControlPlaneDomainsPageModelTests
{
    [Fact]
    public void ConstructorRejectsMissingService()
    {
        Assert.Throws<ArgumentNullException>(() => new DomainsIndexModel(null!));
    }

    [Fact]
    public async Task OnGetDoesNotLoadAllDomains()
    {
        var service = new FakeDomainApplicationService();
        var page = new DomainsIndexModel(service);

        await page.OnGetAsync(CancellationToken.None);

        Assert.Null(service.LastGetAll.Query);
    }

    [Theory]
    [InlineData("key", "key", true)]
    [InlineData("status", "isActive", true)]
    [InlineData("description", "description", true)]
    [InlineData("unknown", "displayName", false)]
    public async Task ListAppliesPagingSearchAndSafeSort(string sort, string expected, bool descending)
    {
        var service = new FakeDomainApplicationService();
        var result = await new DomainsIndexModel(service).OnGetListAsync(2, 500, " sales ", sort, descending);

        Assert.IsType<JsonResult>(result);
        Assert.Equal(2, service.LastGetAll.Query.PagedSettings.PageNumber);
        Assert.Equal(100, service.LastGetAll.Query.PagedSettings.PageSize);
        Assert.Equal("sales", service.LastGetAll.Query.SearchText);
        Assert.Equal(expected, service.LastGetAll.Query.PagedSettings.Sorts.First().Field);
        Assert.Equal(descending, service.LastGetAll.Query.PagedSettings.Sorts.First().Descending);
        Assert.Equal(expected == "key" ? new[] { "key" } : new[] { expected, "key" },
            service.LastGetAll.Query.PagedSettings.Sorts.Select(s => s.Field));
    }

    [Fact]
    public async Task ListClampsPageAndSizeAndDefaultsSearch()
    {
        var service = new FakeDomainApplicationService();
        await new DomainsIndexModel(service).OnGetListAsync(-1, 0);
        Assert.Equal(1, service.LastGetAll.Query.PagedSettings.PageNumber);
        Assert.Equal(1, service.LastGetAll.Query.PagedSettings.PageSize);
        Assert.Equal(string.Empty, service.LastGetAll.Query.SearchText);
    }

    [Fact]
    public async Task ListReturnsGridDataAndQueryIdentity()
    {
        var result = Assert.IsType<JsonResult>(await new DomainsIndexModel(new FakeDomainApplicationService())
            .OnGetListAsync(1, 25, " Sales ", "status", true));
        using var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(result.Value));
        Assert.Equal("Sales", json.RootElement.GetProperty("search").GetString());
        Assert.Equal("status", json.RootElement.GetProperty("sort").GetString());
        Assert.True(json.RootElement.GetProperty("descending").GetBoolean());
        Assert.False(json.RootElement.GetProperty("hasMore").GetBoolean());
        Assert.Equal(1, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal("domain-1", json.RootElement.GetProperty("items")[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task UpsertTrimsInputAndReloadsWhenInvalid()
    {
        var service = new FakeDomainApplicationService();
        var page = new DomainsIndexModel(service)
        {
            Input = new DomainsIndexModel.DomainInput
            {
                DomainId = "domain-1",
                Key = " sales ",
                DisplayName = " Sales ",
                Description = " Domain "
            }
        };

        var result = await page.OnPostUpsertAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("sales", service.LastUpsert.Key);
        Assert.Equal("Sales", service.LastUpsert.DisplayName);
        Assert.Equal("Domain", service.LastUpsert.Description);

        page.ModelState.AddModelError("Input.Key", "Key is required.");
        var invalid = await page.OnPostUpsertAsync(CancellationToken.None);

        Assert.IsType<PageResult>(invalid);
        Assert.Null(service.LastGetAll.Query);
    }

    [Fact]
    public async Task SetActiveDelegatesAndRedirects()
    {
        var service = new FakeDomainApplicationService();
        var page = new DomainsIndexModel(service);

        var result = await page.OnPostSetIsActiveAsync("domain-1", false, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("domain-1", service.LastSetIsActive.DomainId);
        Assert.False(service.LastSetIsActive.IsActive);
    }

    private sealed class FakeDomainApplicationService : IDomainApplicationService
    {
        public (GetDomainsQuery Query, CancellationToken CancellationToken) LastGetAll { get; private set; }

        public UpsertDomainCommand LastUpsert { get; private set; } = new(string.Empty, string.Empty, string.Empty, string.Empty);

        public SetDomainIsActiveCommand LastSetIsActive { get; private set; } = new(string.Empty, true);

        public Task<string> Upsert(UpsertDomainCommand command, CancellationToken cancellationToken = default)
        {
            LastUpsert = command;
            return Task.FromResult("domain-1");
        }

        public Task<bool> SetIsActive(SetDomainIsActiveCommand command, CancellationToken cancellationToken = default)
        {
            LastSetIsActive = command;
            return Task.FromResult(true);
        }

        public Task<DomainModel> GetById(GetDomainByIdQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(new DomainModel { Id = query.DomainId, Key = "sales", DisplayName = "Sales" });

        public Task<ApplicationPagedResult<DomainModel>> GetAll(GetDomainsQuery query, CancellationToken cancellationToken = default)
        {
            LastGetAll = (query, cancellationToken);
            return Task.FromResult(new ApplicationPagedResult<DomainModel>(
                1,
                1,
                1,
                query.PagedSettings.PageSize,
                [
                    new DomainModel
                    {
                        Id = "domain-1",
                        Key = "sales",
                        DisplayName = "Sales",
                        IsActive = true
                    }
                ]));
        }
    }
}
