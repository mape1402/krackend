using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    internal static class MuleActionKeys
    {
        public const string TriggerAction = "TriggerSaga";
        
        public const string BackchannelAction = "BackchannelSaga";

        public const string RemoteCommandDispatchAction = "RemoteCommandDispatch";

        public const string StatisticsBatchAction = "SagaStatisticsBatch";

        public  static ActionKey TriggerActionKey => ActionKey.From(TriggerAction);

        public static ActionKey BackchannelActionKey => ActionKey.From(BackchannelAction);

        public static ActionKey RemoteCommandDispatchActionKey => ActionKey.From(RemoteCommandDispatchAction);

        public static ActionKey StatisticsBatchActionKey => ActionKey.From(StatisticsBatchAction);
    }
}
