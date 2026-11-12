using Microsoft.AspNetCore.Mvc;

namespace Krackend.Sagas.Orchestrations.WebUI.Shell.Grids;

/// <summary>Renders an AG Grid host for a paged catalog.</summary>
public sealed class KrackendAgGridViewComponent : ViewComponent
{
    /// <summary>Renders a paged catalog grid.</summary>
    public IViewComponentResult Invoke(KrackendGridOptions options) => View(options);
}
