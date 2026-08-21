using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Sales.Business.Sales.Models.Requests;
using Sales.Business.Sales.Models.Responses;
using Spider.Pipelines.Core;
using TurtlePath.Spider;

namespace Sales.Api.Controllers
{
    [Route("sales")]
    public sealed class SalesController : BaseController
    {
        private readonly IConfiguration _configuration;

        public SalesController(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        [HttpPost]
        [ProducesResponseType(typeof(SaleResponse), StatusCodes.Status202Accepted)]
        public async Task<ActionResult<SaleResponse>> Create(
            [FromBody] CreateSaleRequest request,
            CancellationToken cancellationToken)
        {
            var orchestrationConfiguration = _configuration.GetRequiredSection("Orchestrations:SaleCreated");
            var orchestrationTopic = orchestrationConfiguration.GetValue<string>("Topic")
                ?? throw new InvalidOperationException("The sale created orchestration topic is not configured.");
            var orchestrationVersion = orchestrationConfiguration.GetValue<string>("Version");

            var response = await Spider
                .AsMediator()
                .UseOrchestration<CreateSaleRequest, SaleResponse>(
                    (req, res) => new
                    {
                        res.SaleId,
                        req.CustomerId,
                        res.Total,
                        DateTime.UtcNow
                    },
                    orchestrationTopic,
                    orchestrationVersion)
                .Send(request, cancellationToken);

            return Accepted(response);
        }
    }
}
