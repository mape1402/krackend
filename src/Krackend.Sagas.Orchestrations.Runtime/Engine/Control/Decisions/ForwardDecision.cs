using Krackend.Sagas.Orchestrations.Runtime.Engine.Actions;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal class ForwardDecision : IDecision
    {
        private readonly ForwardContext _context;

        public ForwardDecision(ForwardContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public ITaskAction HandsOn()
            => new InvokeRemoteCommandAction(new InvokeRemoteCommandContext(_context.ServiceProvider));
    }
}
