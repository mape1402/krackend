using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mule;

namespace Krackend.Demo;

public sealed class DemoEmailDeliveryExecutor(
    DemoDbContext db,
    DemoOutbox outbox,
    IConfiguration configuration)
{
    public async Task ExecuteAsync(DemoEmailDelivery work, CancellationToken cancellationToken)
    {
        var email = await db.Emails.AsNoTracking().SingleAsync(x => x.Id == work.EmailId, cancellationToken);
        if (email.Status != "Delivered")
        {
            DemoService.ValidateEmail(email.Recipient);
            var host = configuration["Demo:SmtpHost"] ?? "localhost";
            if (host is not ("localhost" or "127.0.0.1" or "mailpit"))
                throw new InvalidOperationException("The demo only delivers to a local SMTP capture service.");
            using var smtp = new SmtpClient(host, int.Parse(configuration["Demo:SmtpPort"] ?? "1025"));
            using var message = new MailMessage("demo@krackend.test", email.Recipient, "Krackend demo " + email.SaleId, email.Body);
            message.Headers.Add("X-Demo-Message-Id", email.Id);
            await smtp.SendMailAsync(message, cancellationToken);
        }
        // One atomic update is enough on the healthy path. The previous version
        // committed Attempts separately and then opened another transaction for
        // Delivered, adding a full log flush to every saga.
        await db.Emails.Where(x => x.Id == email.Id).ExecuteUpdateAsync(u => u
            .SetProperty(x => x.Attempts, x => x.Attempts + 1)
            .SetProperty(x => x.Status, "Delivered")
            .SetProperty(x => x.DeliveredOnUtc, DateTime.UtcNow), cancellationToken);
        await outbox.AddAsync("DemoPublish", "reply:" + work.Callback.Metadata.DispatchId + ":" + work.Callback.Metadata.Attempt,
            work.Callback, work.Callback.Metadata.CorrelationId, cancellationToken);
        await outbox.NotifyCommittedAsync(cancellationToken);
    }
}

[MuleAction("DemoDeliverEmail")]
public sealed class DemoDeliverEmailAction(DemoEmailDeliveryExecutor executor) : IMuleAction<DemoEmailDelivery>
{
    public ValueTask ExecuteAsync(MuleActionContext<DemoEmailDelivery> context, CancellationToken cancellationToken)
        => new(executor.ExecuteAsync(context.Payload, cancellationToken));
}

