using System.Text.Json;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using MetadataIndexModel = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.Metadata.IndexModel;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class ControlPlaneMetadataCatalogTests
{
    [Fact]
    public void RejectsMissingService() => Assert.Throws<ArgumentNullException>(() => new MetadataIndexModel(null!));

    [Fact]
    public async Task InitialPageDoesNotReadAllDescriptors()
    {
        var service = Substitute.For<IMetadataDescriptorApplicationService>();
        await new MetadataIndexModel(service).OnGetAsync();
        await service.DidNotReceive().GetAll(Arg.Any<GetMetadataDescriptorsQuery>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("key", "key")]
    [InlineData("sourceKey", "sourceKey")]
    [InlineData("description", "description")]
    [InlineData("invalid", "displayName")]
    public async Task ListQueriesOneBoundedPageWithStableSort(string sort, string expected)
    {
        var service = CreateService();
        var result = Assert.IsType<JsonResult>(await new MetadataIndexModel(service).OnGetListAsync(2, 500, " source ", sort, true));
        await service.Received(1).GetAll(Arg.Is<GetMetadataDescriptorsQuery>(query =>
            query.PagedSettings.PageNumber == 2 && query.PagedSettings.PageSize == 100 && query.SearchText == "source" &&
            query.PagedSettings.Sorts.First().Field == expected && query.PagedSettings.Sorts.First().Descending), Arg.Any<CancellationToken>());
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        var item = json.RootElement.GetProperty("items")[0];
        Assert.Equal("event.metadata", item.GetProperty("sourceKey").GetString());
        Assert.False(item.TryGetProperty("schemaJson", out _));
        Assert.Equal("source", json.RootElement.GetProperty("search").GetString());
    }

    [Fact]
    public async Task ListClampsInvalidBounds()
    {
        var service = CreateService();
        await new MetadataIndexModel(service).OnGetListAsync(-2, -1);
        await service.Received(1).GetAll(Arg.Is<GetMetadataDescriptorsQuery>(q =>
            q.PagedSettings.PageNumber == 1 && q.PagedSettings.PageSize == 1 && q.SearchText == string.Empty), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteDelegatesAndRedirects()
    {
        var service = CreateService();
        var result = await new MetadataIndexModel(service).OnPostDeleteAsync("descriptor");
        Assert.IsType<RedirectToPageResult>(result);
        await service.Received(1).Delete(Arg.Is<DeleteMetadataDescriptorCommand>(x => x.MetadataDescriptorId == "descriptor"), Arg.Any<CancellationToken>());
    }

    private static IMetadataDescriptorApplicationService CreateService()
    {
        var service = Substitute.For<IMetadataDescriptorApplicationService>();
        service.GetAll(Arg.Any<GetMetadataDescriptorsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ApplicationPagedResult<MetadataDescriptorModel>(2, 2, 1, 25,
                [new MetadataDescriptorModel { Id = "descriptor", Key = "event", SourceKey = "event.metadata", DisplayName = "Event", SchemaJson = "{schema}" }]));
        return service;
    }
}
