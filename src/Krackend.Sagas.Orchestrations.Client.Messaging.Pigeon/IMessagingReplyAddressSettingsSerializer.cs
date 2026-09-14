namespace Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

internal interface IMessagingReplyAddressSettingsSerializer
{
    MessagingReplyAddressSettings Deserialize(string payload);
}
