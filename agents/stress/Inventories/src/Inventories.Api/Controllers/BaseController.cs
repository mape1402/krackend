using Microsoft.AspNetCore.Mvc;
using Pelican.Mediator;
using Spider.Pipelines.Core;

namespace Inventories.Api.Controllers
{
    /// <summary>
    /// Provides a base API controller with access to the mediator.
    /// </summary>
    [Route("[controller]")]
    [ApiController]
    public class BaseController : ControllerBase
    {
        private IMediator _mediator;
        private ISpider _spider;

        /// <summary>
        /// Gets the mediator resolved from the current request services.
        /// </summary>
        public IMediator Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<IMediator>();

        /// <summary>
        /// Gets the spider instance from the current context.
        /// </summary>
        public ISpider Spider => _spider ??= HttpContext.RequestServices.GetRequiredService<ISpider>();
    }
}
