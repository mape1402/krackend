using Krackend.Sagas.Orchestrations.Runtime.Engine.Actions;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal class RetryDecision : IDecision
    {
        private readonly RetryContext _context;

        public RetryDecision(RetryContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public ITaskAction HandsOn()
            => new InvokeRemoteCommandAction(new InvokeRemoteCommandContext(_context.ServiceProvider));
    }
}
