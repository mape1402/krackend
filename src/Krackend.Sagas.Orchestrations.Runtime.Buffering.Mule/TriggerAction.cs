using Microsoft.Extensions.Logging;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    [MuleAction(MuleActionKeys.TriggerAction)]
    public class TriggerAction : IMuleAction<WorkItem>
    {
        private readonly ILogger<TriggerAction> _logger;

        public TriggerAction(ILogger<TriggerAction> logger)
        {
            _logger = logger;
        }

        public ValueTask ExecuteAsync(MuleActionContext<WorkItem> context, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Promote to engine a new SAGA instance.");
            return ValueTask.CompletedTask;
        }
    }
}
