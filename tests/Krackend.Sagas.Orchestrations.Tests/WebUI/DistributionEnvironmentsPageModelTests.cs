using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Environments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class DistributionEnvironmentsPageModelTests
{
    [Fact]
    public async Task OnGetListLoadsOnlyTheRequestedPage()
    {
        var service = Substitute.For<IDistributionEnvironmentApplicationService>();
        service
            .Search(Arg.Any<ApplicationPagedSettings>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new ApplicationPagedResult<DistributionEnvironmentModel>
            {
                PageNumber = 2,
                PageSize = 25,
                TotalRows = 26,
                TotalPages = 2,
                Rows =
                [
                    new DistributionEnvironmentModel
                    {
                        Id = "environment-1",
                        Name = "Production",
                        Code = "prod",
                        Description = "Primary",
                        IsEnabled = true,
                        CreatedAtUtc = DateTime.UtcNow
                    }
                ]
            });
        var model = CreateModel(service);

        model.OnGet();
        var result = await model.OnGetListAsync(2, 25, "prod", "key", true);

        Assert.IsType<JsonResult>(result);
        await service.Received(1).Search(
            Arg.Is<ApplicationPagedSettings>(settings => settings.PageNumber == 2 && settings.PageSize == 25),
            "prod", "key", true, Arg.Any<CancellationToken>());
        await service.DidNotReceiveWithAnyArgs().GetAll(default!, default);
    }

    [Fact]
    public async Task OnPostUpsertReloadsPageWhenModelStateIsInvalid()
    {
        var service = Substitute.For<IDistributionEnvironmentApplicationService>();
        var model = CreateModel(service);
        model.ModelState.AddModelError(nameof(EnvironmentInput.Name), "Name is required.");

        var result = await model.OnPostUpsertAsync();

        Assert.IsType<PageResult>(result);
        await service.DidNotReceiveWithAnyArgs().GetAll(default!, default);
        await service.DidNotReceiveWithAnyArgs().Upsert(default!, default);
    }

    [Theory]
    [InlineData(true, 503)]
    [InlineData(false, 500)]
    public async Task OnGetListReturnsFriendlyErrorWhenSearchFails(bool unsupported, int status)
    {
        var service = Substitute.For<IDistributionEnvironmentApplicationService>();
        service.Search(Arg.Any<ApplicationPagedSettings>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<ApplicationPagedResult<DistributionEnvironmentModel>>(
                unsupported ? new NotSupportedException() : new InvalidOperationException()));
        var result = await CreateModel(service).OnGetListAsync();

        Assert.Equal(status, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task OnGetListPropagatesCancellation()
    {
        var service = Substitute.For<IDistributionEnvironmentApplicationService>();
        service.Search(Arg.Any<ApplicationPagedSettings>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<ApplicationPagedResult<DistributionEnvironmentModel>>(new OperationCanceledException()));
        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateModel(service).OnGetListAsync());
    }

    [Fact]
    public async Task OnPostUpsertTrimsInputSavesAndSetsSuccessMessage()
    {
        var service = Substitute.For<IDistributionEnvironmentApplicationService>();
        service
            .Upsert(Arg.Any<UpsertDistributionEnvironmentInput>(), Arg.Any<CancellationToken>())
            .Returns("environment-1");
        var model = CreateModel(service);
        model.Input = new EnvironmentInput
        {
            EnvironmentId = "environment-1",
            Name = " Production ",
            Code = " prod ",
            Description = " Primary environment ",
            IsEnabled = true
        };

        var result = await model.OnPostUpsertAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Environment saved", model.TempData["OrchestratorMessage.Title"]);
        Assert.Equal("success", model.TempData["OrchestratorMessage.Type"]);
        await service.Received(1).Upsert(
            Arg.Is<UpsertDistributionEnvironmentInput>(input =>
                input.EnvironmentId == "environment-1" &&
                input.Name == "Production" &&
                input.Code == "prod" &&
                input.Description == "Primary environment" &&
                input.IsEnabled),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnPostUpsertCapturesServiceErrorsAsPageMessages()
    {
        var service = Substitute.For<IDistributionEnvironmentApplicationService>();
        service
            .Upsert(Arg.Any<UpsertDistributionEnvironmentInput>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<string>(new InvalidOperationException("duplicate code")));
        var model = CreateModel(service);
        model.Input = new EnvironmentInput
        {
            Name = "Production",
            Code = "prod",
            Description = null!,
            IsEnabled = false
        };

        var result = await model.OnPostUpsertAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Environment save failed", model.TempData["OrchestratorMessage.Title"]);
        Assert.Equal("duplicate code", model.TempData["OrchestratorMessage.Body"]);
        Assert.Equal("error", model.TempData["OrchestratorMessage.Type"]);
    }

    private static IndexModel CreateModel(IDistributionEnvironmentApplicationService service)
        => new(service)
        {
            PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext
            {
                HttpContext = new DefaultHttpContext()
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>())
        };
}
