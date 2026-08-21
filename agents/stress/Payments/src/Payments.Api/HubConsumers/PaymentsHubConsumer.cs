using Payments.Business.Payments.Models.Requests;
using Payments.Business.Payments.Models.Responses;
using Pigeon.Messaging.Consuming.Dispatching;
using Spider.Pipelines.Core;
using TurtlePath.Spider;

namespace Payments.Api.HubConsumers
{
    public sealed class PaymentsHubConsumer : BaseHubConsumer
    {
        [Consumer("commands.payments.payment.capture.", "1.1.0")]
        [Consumer("commands.payments.payment.capture.", "1.0.0")]
        [Consumer("tasks.payments.capture.requested", "1.0.0")]
        public async Task Consume(CapturePaymentRequest request, CancellationToken cancellationToken)
        {
            await Spider
                .AsMediator()
                .UseOrchestration<CapturePaymentRequest, CapturePaymentResponse>()
                .Send(request, cancellationToken);
        }
    }
}
