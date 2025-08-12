using Krackend.Sagas.Orchestration.Controller.Configuration.Builders;

namespace Krackend.Client.Tests.Orchestrations
{
    public class DemoOrchestration : IOrchestration
    {
        public void Configure(IRoadmapBuilder builder)
        {
            builder
                .Set("opportunities.create_opportunity", "Create Opportunity")
                .Description("Creates a new opportunity from legacy sourcing.")
                .AsActive()
                .SubscribeTo("events.opportunities.new_opportunity")
                .ListenTo("orchestrations.opportunities.new_opportunity")
                .Next(stage =>
                {
                    stage
                        .Set("opportunity_created", "Legacy Opportunity Created")
                        .Description("Event triggered when a legacy opportunity is created.");
                })
                .Next(stage =>
                {
                    stage
                        .Set("send_opportunity_to_manager", "Send Opportunity to Manager")
                        .Description("Sends the created opportunity to the manager for review.")
                        .StepForward(config =>
                        {
                            config
                                .ResolveTo("commands.opportunities.create_opportunity");
                        })
                        .Compensation(config =>
                        {
                            config
                                .ResolveTo("commands.opportunities.cancel_opportunity");
                        });
                });
                //.Next(stage =>
                //{
                //    stage
                //        .Set("dispatch_to_webhook", "Dispatch to Webhook")
                //        .Description("Dispatches the created opportunity to a webhook for external further processing.")
                //        .StepForward(config =>
                //        {
                //            config
                //                .ResolveTo("commands.webhook_dispatcher.dispatch_webhook");
                //        });
                //});
        }
    }
}
