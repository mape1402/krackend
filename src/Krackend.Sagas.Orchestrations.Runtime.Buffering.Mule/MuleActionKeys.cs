using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    internal static class MuleActionKeys
    {
        public const string TriggerAction = "TriggerSaga";
        
        public const string BackchannelAction = "BackchannelSaga";

        public  static ActionKey TriggerActionKey => ActionKey.From(TriggerAction);

        public static ActionKey BackchannelActionKey => ActionKey.From(BackchannelAction);
    }
}
