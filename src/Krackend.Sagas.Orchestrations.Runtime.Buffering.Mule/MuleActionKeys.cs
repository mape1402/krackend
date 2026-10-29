using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    internal static class MuleActionKeys
    {
        public const string TriggerAction = "TriggerSaga";
        
        public const string BackchannelAction = "BackchannelSaga";

        public const string RemoteCommandDispatchAction = "RemoteCommandDispatch";

        public const string StatisticsBatchAction = "SagaStatisticsBatch";

        public const string TaskResponseTimeoutAction = "TaskResponseTimeout";

        public const string TriggerLane = "runtime-trigger";

        public const string BackchannelLane = "runtime-backchannel";

        public const string DispatchLane = "runtime-dispatch";

        public const string StatisticsLane = "runtime-statistics";

        public const string TimeoutLane = "runtime-timeout";

        public  static ActionKey TriggerActionKey => ActionKey.From(TriggerAction);

        public static ActionKey BackchannelActionKey => ActionKey.From(BackchannelAction);

        public static ActionKey RemoteCommandDispatchActionKey => ActionKey.From(RemoteCommandDispatchAction);

        public static ActionKey StatisticsBatchActionKey => ActionKey.From(StatisticsBatchAction);

        public static ActionKey TaskResponseTimeoutActionKey => ActionKey.From(TaskResponseTimeoutAction);
    }
}
