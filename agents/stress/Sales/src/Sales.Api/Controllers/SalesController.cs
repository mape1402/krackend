using Microsoft.AspNetCore.Mvc;
using Sales.Business.Sales.Events;
using Sales.Business.Sales.Models.Requests;
using Sales.Business.Sales.Models.Responses;
using Spider.Pipelines.Core;
using TurtlePath.Spider;

namespace Sales.Api.Controllers
{
    [Route("sales")]
    public sealed class SalesController : BaseController
    {
        [HttpPost]
        [ProducesResponseType(typeof(SaleResponse), StatusCodes.Status202Accepted)]
        public async Task<ActionResult<SaleResponse>> Create(
            [FromBody] CreateSaleRequest request,
            CancellationToken cancellationToken)
        {
            var response = await Spider
                .AsMediator()
                .UseOrchestration<CreateSaleRequest, SaleResponse>(
                    (req, res) => new SaleCreatedMessage
                    {
                        SaleId = res.SaleId,
                        CustomerId = req.CustomerId,
                        Total = res.Total,
                        OccurredOnUtc = DateTime.UtcNow
                    },
                    "events.sales.sale.created")
                .Send(request, cancellationToken);

            return Accepted(response);
        }
    }
}
