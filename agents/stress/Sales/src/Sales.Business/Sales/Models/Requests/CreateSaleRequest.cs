using Pelican.Mediator;
using Sales.Business.Sales.Models.Responses;

namespace Sales.Business.Sales.Models.Requests
{
    public sealed class CreateSaleRequest : IRequest<SaleResponse>
    {
        public string SaleId { get; set; }
        public string RequestId { get; set; }
        public string TestRunId { get; set; }
        public List<Krackend.Demo.DemoLine> Lines { get; set; } = [];

        public string CustomerId { get; set; }

        public decimal Total { get; set; }
    }
}
