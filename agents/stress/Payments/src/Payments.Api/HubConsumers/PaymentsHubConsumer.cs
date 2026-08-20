using Payments.Business.Payments.Messages;
using Payments.Business.Payments.Models.Requests;
using Payments.Business.Payments.Models.Responses;
using Pigeon.Messaging.Consuming.Dispatching;
using Spider.Pipelines.Core;
using TurtlePath.Spider;

namespace Payments.Api.HubConsumers
{
    public sealed class PaymentsHubConsumer : BaseHubConsumer
    {
        [Consumer("tasks.payments.capture.requested", "1.0.0", "payments")]
        public Task Consume(CapturePaymentMessage message, CancellationToken cancellationToken)
        {
            return Spider
                .AsMediator()
                .UseOrchestration<CapturePaymentRequest, CapturePaymentResponse>()
                .Send(new CapturePaymentRequest
                {
                    SaleId = message.SaleId,
                    CustomerId = message.CustomerId,
                    Total = message.Total
                }, cancellationToken);
        }
    }
}
