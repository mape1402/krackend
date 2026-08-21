using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Orchestrations;
using Sales.Business.Sales.Models.Requests;
using Sales.Business.Sales.Models.Responses;

namespace Sales.Api.Controllers
{
    /// <summary>
    /// Handles sale commands and sale-created orchestration triggers.
    /// </summary>
    [ApiVersion(1.0)]
    [ApiVersion(1.1)]
    [ApiVersion(1.2)]
    [Route("sales")]
    public sealed class SalesController : BaseController
    {
        private readonly ISaleCreatedOrchestrationDispatcher _dispatcher;

        /// <summary>
        /// Initializes a new instance of the <see cref="SalesController"/> class.
        /// </summary>
        public SalesController(ISaleCreatedOrchestrationDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <summary>
        /// Creates a sale and publishes the sale-created orchestration message for artifact version 1.0.0.
        /// </summary>
        [HttpPost]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(SaleResponse), StatusCodes.Status202Accepted)]
        public async Task<ActionResult<SaleResponse>> CreateV1(
            [FromBody] CreateSaleRequest request,
            CancellationToken cancellationToken)
        {
            var response = await _dispatcher.DispatchAsync(request, "1.0.0", cancellationToken);

            return Accepted(response);
        }

        /// <summary>
        /// Creates a sale and publishes the sale-created orchestration message for artifact version 1.1.0.
        /// </summary>
        [HttpPost]
        [MapToApiVersion(1.1)]
        [ProducesResponseType(typeof(SaleResponse), StatusCodes.Status202Accepted)]
        public async Task<ActionResult<SaleResponse>> CreateV1_1(
            [FromBody] CreateSaleRequest request,
            CancellationToken cancellationToken)
        {
            var response = await _dispatcher.DispatchAsync(request, "1.1.0", cancellationToken);

            return Accepted(response);
        }

        /// <summary>
        /// Creates a sale and publishes the sale-created orchestration message for artifact version 1.2.0.
        /// </summary>
        [HttpPost]
        [MapToApiVersion(1.2)]
        [ProducesResponseType(typeof(SaleResponse), StatusCodes.Status202Accepted)]
        public async Task<ActionResult<SaleResponse>> CreateV1_2(
            [FromBody] CreateSaleRequest request,
            CancellationToken cancellationToken)
        {
            var response = await _dispatcher.DispatchAsync(request, "1.2.0", cancellationToken);

            return Accepted(response);
        }
    }
}
