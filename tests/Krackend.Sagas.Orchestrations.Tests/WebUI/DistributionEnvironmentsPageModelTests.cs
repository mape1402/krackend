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
    public async Task OnGetLoadsDistributionEnvironments()
    {
        var service = Substitute.For<IDistributionEnvironmentApplicationService>();
        service
            .GetAll(Arg.Any<ApplicationPagedSettings>(), Arg.Any<CancellationToken>())
            .Returns(new ApplicationPagedResult<DistributionEnvironmentModel>
            {
                PageNumber = 1,
                PageSize = 200,
                TotalRows = 1,
                TotalPages = 1,
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

        await model.OnGetAsync();

        Assert.Equal("Production", Assert.Single(model.Rows).Name);
        await service.Received(1).GetAll(
            Arg.Is<ApplicationPagedSettings>(settings => settings.PageNumber == 1 && settings.PageSize == 200),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnPostUpsertReloadsPageWhenModelStateIsInvalid()
    {
        var service = Substitute.For<IDistributionEnvironmentApplicationService>();
        service
            .GetAll(Arg.Any<ApplicationPagedSettings>(), Arg.Any<CancellationToken>())
            .Returns(new ApplicationPagedResult<DistributionEnvironmentModel>
            {
                Rows = [new DistributionEnvironmentModel { Id = "environment-1", Name = "QA", Code = "qa" }]
            });
        var model = CreateModel(service);
        model.ModelState.AddModelError(nameof(EnvironmentInput.Name), "Name is required.");

        var result = await model.OnPostUpsertAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal("QA", Assert.Single(model.Rows).Name);
        await service.DidNotReceiveWithAnyArgs().Upsert(default!, default);
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
