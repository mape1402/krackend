using Microsoft.Extensions.Logging;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    [MuleAction(MuleActionKeys.BackchannelAction)]
    public class BackchannelAction : IMuleAction<WorkItem>
    {
        private readonly ILogger<BackchannelAction> _logger;

        public BackchannelAction(ILogger<BackchannelAction> logger)
        {
            _logger = logger;
        }

        public ValueTask ExecuteAsync(MuleActionContext<WorkItem> context, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Process a response from backchannel communication.....");
            return ValueTask.CompletedTask;
        }
    }
}
