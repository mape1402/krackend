namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

using System.Diagnostics;
using Krackend.Sagas.Orchestrations.WebUI.Shell.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;

public sealed class WebUIShellErrorPageModelTests
{
    [Fact]
    public void OnGetUsesHttpTraceIdentifierWhenNoActivityIsAvailable()
    {
        var model = new ErrorModel();
        model.PageContext = new PageContext
        {
            HttpContext = new DefaultHttpContext
            {
                TraceIdentifier = "trace-1"
            }
        };

        model.OnGet();

        Assert.Equal("trace-1", model.RequestId);
        Assert.True(model.ShowRequestId);
    }

    [Fact]
    public void OnGetUsesTheCurrentActivityIdentifierWhenItIsAvailable()
    {
        using var activity = new Activity("request").Start();
        var model = new ErrorModel();
        model.PageContext = new PageContext
        {
            HttpContext = new DefaultHttpContext
            {
                TraceIdentifier = "http-trace"
            }
        };

        model.OnGet();

        Assert.Equal(activity.Id, model.RequestId);
    }
}
