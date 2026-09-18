namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;
using Microsoft.EntityFrameworkCore;
using static Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure.RealMessagingArtifactFactory;

[Collection(RuntimeRealInfrastructureCollection.Name)]
public sealed class RuntimeRealInfrastructureE2ETests
{
    private readonly RuntimeRealInfrastructureFixture _infrastructure;

    public RuntimeRealInfrastructureE2ETests(RuntimeRealInfrastructureFixture infrastructure)
    {
        _infrastructure = infrastructure;
    }

    [Fact(Timeout = 180_000)]
    public async Task RuntimeCompletesThreeStageMessagingSagaThroughRealRabbitSqlAndRedis()
    {
        var suffix = NewSuffix();
        var version = new SemanticVersion(2, 0, 0);
        var triggerTopic = Topic(suffix, "events.sales.sale.created");
        var validateTopic = Topic(suffix, "commands.sales.sale.validate");
        var inventoryTopic = Topic(suffix, "commands.inventories.stock.reserve");
        var paymentTopic = Topic(suffix, "commands.payments.payment.capture");
        var notificationTopic = Topic(suffix, "commands.notifications.sale.send");
        var orchestrationKey = Topic(suffix, "sales.sale.created");
        var parallelGroupId = Id.New();

        await using var services = await RealMessagingServiceHost.StartAsync(
            _infrastructure,
            Endpoints(version, validateTopic, inventoryTopic, paymentTopic, notificationTopic));
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure);

        services.Scenario.Enqueue(validateTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("validated", true))));
        services.Scenario.Enqueue(inventoryTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("reserved", true))));
        services.Scenario.Enqueue(paymentTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("captured", true))));
        services.Scenario.Enqueue(notificationTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("notified", true))));

        var artifact = Artifact(
            orchestrationKey,
            version,
            $"real-happy-{suffix}",
            [EventTrigger(triggerTopic, version)],
            Stage("sale-created", 1, MessagingTask(validateTopic, 1, version)),
            StageWithGraph(
                "fulfillment",
                2,
                [new ParallelGroupArtifact(parallelGroupId, ParallelJoinPolicy.WaitAll, null)],
                [],
                MessagingTask(inventoryTopic, 1, version, executionMode: TaskExecutionMode.Parallel, parallelGroupId: parallelGroupId),
                MessagingTask(paymentTopic, 2, version, executionMode: TaskExecutionMode.Parallel, parallelGroupId: parallelGroupId)),
            Stage("confirmation", 3, MessagingTask(notificationTopic, 1, version)));

        var deployment = await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        var correlationId = $"corr-{suffix}-happy";
        await runtime.PublishTriggerAsync(
            triggerTopic,
            version.ToString(),
            BusinessPayload(("saleId", $"S-{suffix}"), ("amount", 1200)),
            correlationId);

        var instance = await runtime.WaitForInstanceStatusAsync(
            correlationId,
            OrchestrationInstanceStatus.Completed,
            TimeSpan.FromSeconds(90));
        var invocations = await WaitForInvocationCountAsync(services.Scenario, 4);
        var attempts = await runtime.QueryAsync(dbContext => dbContext.TaskExecutionAttempts
            .AsNoTracking()
            .ToArrayAsync());
        var stages = await runtime.QueryAsync(dbContext => dbContext.StageExecutions
            .AsNoTracking()
            .Where(stage => stage.OrchestrationInstanceId == instance.Id)
            .ToArrayAsync());

        Assert.Equal(deployment.RuntimeArtifactId, instance.RuntimeOrchestrationArtifactId.ToString());
        Assert.All(stages, stage => Assert.Equal(StageExecutionStatus.Completed, stage.Status));
        Assert.All(invocations, invocation =>
        {
            AssertBusinessPayloadWasNotWrapped(invocation.Payload);
            Assert.NotNull(invocation.MessageMetadata.ReplyAddress);
            Assert.Contains($"orchestrations.{orchestrationKey}", invocation.MessageMetadata.ReplyAddress!.SettingsPayload);
        });
        Assert.Contains(attempts, attempt =>
            attempt.ResponsePayload?["reserved"]?.GetValue<bool>() == true &&
            attempt.Metadata["ExecutionSucceeded"]?.GetValue<bool>() == true);
    }

    [Fact(Timeout = 360_000)]
    public async Task RuntimeCompletesDeepMultiStageMessagingSagaWithMoreServices()
    {
        var suffix = NewSuffix();
        var version = new SemanticVersion(2, 5, 0);
        var triggerTopic = Topic(suffix, "events.sales.deep.created");
        var validateTopic = Topic(suffix, "commands.sales.deep.validate");
        var pricingTopic = Topic(suffix, "commands.pricing.deep.calculate");
        var inventoryTopic = Topic(suffix, "commands.inventories.deep.reserve");
        var paymentTopic = Topic(suffix, "commands.payments.deep.capture");
        var riskTopic = Topic(suffix, "commands.risk.deep.evaluate");
        var invoiceTopic = Topic(suffix, "commands.billing.deep.invoice");
        var shippingTopic = Topic(suffix, "commands.shipping.deep.book");
        var loyaltyTopic = Topic(suffix, "commands.loyalty.deep.accrue");
        var notificationTopic = Topic(suffix, "commands.notifications.deep.send");
        var orchestrationKey = Topic(suffix, "sales.deep.created");
        var fulfillmentGroupId = Id.New();

        await using var services = await RealMessagingServiceHost.StartAsync(
            _infrastructure,
            Endpoints(
                version,
                validateTopic,
                pricingTopic,
                inventoryTopic,
                paymentTopic,
                riskTopic,
                invoiceTopic,
                shippingTopic,
                loyaltyTopic,
                notificationTopic));
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure);

        services.Scenario.Enqueue(validateTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("validated", true))));
        services.Scenario.Enqueue(pricingTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("priced", true))));
        services.Scenario.Enqueue(inventoryTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("reserved", true))));
        services.Scenario.Enqueue(paymentTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("captured", true))));
        services.Scenario.Enqueue(riskTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("riskApproved", true))));
        services.Scenario.Enqueue(invoiceTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("invoiced", true))));
        services.Scenario.Enqueue(shippingTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("shipmentBooked", true))));
        services.Scenario.Enqueue(loyaltyTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("pointsAccrued", true))));
        services.Scenario.Enqueue(notificationTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("notified", true))));

        var artifact = Artifact(
            orchestrationKey,
            version,
            $"real-deep-{suffix}",
            [EventTrigger(triggerTopic, version)],
            Stage("sale-validation", 1, MessagingTask(validateTopic, 1, version)),
            Stage("pricing", 2, MessagingTask(pricingTopic, 1, version)),
            StageWithGraph(
                "fulfillment",
                3,
                [new ParallelGroupArtifact(fulfillmentGroupId, ParallelJoinPolicy.WaitAll, null)],
                [],
                MessagingTask(inventoryTopic, 1, version, executionMode: TaskExecutionMode.Parallel, parallelGroupId: fulfillmentGroupId),
                MessagingTask(paymentTopic, 2, version, executionMode: TaskExecutionMode.Parallel, parallelGroupId: fulfillmentGroupId),
                MessagingTask(riskTopic, 3, version, executionMode: TaskExecutionMode.Parallel, parallelGroupId: fulfillmentGroupId)),
            Stage("invoicing", 4, MessagingTask(invoiceTopic, 1, version)),
            Stage("shipment", 5, MessagingTask(shippingTopic, 1, version)),
            Stage(
                "post-sale",
                6,
                MessagingTask(loyaltyTopic, 1, version),
                MessagingTask(notificationTopic, 2, version)));

        await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        var correlationId = $"corr-{suffix}-deep";
        await runtime.PublishTriggerAsync(
            triggerTopic,
            version.ToString(),
            BusinessPayload(("saleId", $"S-{suffix}"), ("amount", 1550)),
            correlationId);

        var instance = await runtime.WaitForInstanceStatusAsync(
            correlationId,
            OrchestrationInstanceStatus.Completed,
            TimeSpan.FromSeconds(120));
        var invocations = await WaitForInvocationCountAsync(services.Scenario, 9);
        var stages = await runtime.QueryAsync(dbContext => dbContext.StageExecutions
            .AsNoTracking()
            .Where(stage => stage.OrchestrationInstanceId == instance.Id)
            .OrderBy(stage => stage.Order)
            .ToArrayAsync());
        var taskExecutions = await runtime.QueryAsync(dbContext => dbContext.TaskExecutions
            .AsNoTracking()
            .Where(task => task.OrchestrationInstanceId == instance.Id)
            .ToArrayAsync());

        Assert.Equal(6, stages.Length);
        Assert.Equal(9, taskExecutions.Length);
        Assert.All(stages, stage => Assert.Equal(StageExecutionStatus.Completed, stage.Status));
        Assert.All(taskExecutions, task => Assert.Equal(TaskExecutionStatus.Completed, task.Status));
        Assert.All(invocations, invocation => AssertBusinessPayloadWasNotWrapped(invocation.Payload));
    }

    [Fact(Timeout = 180_000)]
    public async Task RuntimeRetriesTransientServiceFailureAndCompletesThroughRealMessaging()
    {
        var suffix = NewSuffix();
        var version = new SemanticVersion(2, 1, 0);
        var triggerTopic = Topic(suffix, "events.inventory.retry.requested");
        var inventoryTopic = Topic(suffix, "commands.inventories.stock.reserve");
        var orchestrationKey = Topic(suffix, "inventory.retry");

        await using var services = await RealMessagingServiceHost.StartAsync(
            _infrastructure,
            Endpoints(version, inventoryTopic));
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure);

        services.Scenario.Enqueue(
            inventoryTopic,
            RealMessagingServiceOutcome.Failure(
                "InventoryTransientFailure",
                "Inventory dependency was temporarily unavailable.",
                isRetryableCandidate: true));
        services.Scenario.Enqueue(inventoryTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("reserved", true))));

        var artifact = Artifact(
            orchestrationKey,
            version,
            $"real-retry-{suffix}",
            [EventTrigger(triggerTopic, version)],
            Stage(
                "reservation",
                1,
                MessagingTask(
                    inventoryTopic,
                    1,
                    version,
                    retryPolicy: RetryPolicy(1, "InventoryTransientFailure"))));

        await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        var correlationId = $"corr-{suffix}-retry";
        await runtime.PublishTriggerAsync(
            triggerTopic,
            version.ToString(),
            BusinessPayload(("saleId", $"S-{suffix}")),
            correlationId);

        await runtime.WaitForInstanceStatusAsync(correlationId, OrchestrationInstanceStatus.Completed);
        await WaitForInvocationCountAsync(services.Scenario, inventoryTopic, 2);
        var attempts = await AttemptsForTaskAsync(runtime, inventoryTopic);

        Assert.Equal(2, attempts.Length);
        Assert.Equal(TaskExecutionStatus.Failed, attempts[0].Status);
        Assert.Equal("InventoryTransientFailure", attempts[0].ErrorCode);
        Assert.Equal(TaskExecutionStatus.Completed, attempts[1].Status);
    }

    [Fact(Timeout = 210_000)]
    public async Task RuntimeRetriesUntilConfiguredBudgetAndCompletesWhenLaterAttemptSucceeds()
    {
        var suffix = NewSuffix();
        var version = new SemanticVersion(2, 6, 0);
        var triggerTopic = Topic(suffix, "events.retry.budget.requested");
        var reservationTopic = Topic(suffix, "commands.inventories.retry.budget.reserve");
        var confirmationTopic = Topic(suffix, "commands.notifications.retry.budget.confirm");
        var orchestrationKey = Topic(suffix, "retry.budget");

        await using var services = await RealMessagingServiceHost.StartAsync(
            _infrastructure,
            Endpoints(version, reservationTopic, confirmationTopic));
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure);

        services.Scenario.Enqueue(
            reservationTopic,
            RealMessagingServiceOutcome.Failure("TransientFailure", "First transient failure.", isRetryableCandidate: true));
        services.Scenario.Enqueue(
            reservationTopic,
            RealMessagingServiceOutcome.Failure("TransientFailure", "Second transient failure.", isRetryableCandidate: true));
        services.Scenario.Enqueue(reservationTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("reserved", true))));
        services.Scenario.Enqueue(confirmationTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("confirmed", true))));

        var artifact = Artifact(
            orchestrationKey,
            version,
            $"real-retry-budget-{suffix}",
            [EventTrigger(triggerTopic, version)],
            Stage(
                "reservation",
                1,
                MessagingTask(
                    reservationTopic,
                    1,
                    version,
                    retryPolicy: RetryPolicy(2, "TransientFailure"))),
            Stage("confirmation", 2, MessagingTask(confirmationTopic, 1, version)));

        await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        var correlationId = $"corr-{suffix}-retry-budget";
        await runtime.PublishTriggerAsync(
            triggerTopic,
            version.ToString(),
            BusinessPayload(("saleId", $"S-{suffix}")),
            correlationId);

        await runtime.WaitForInstanceStatusAsync(
            correlationId,
            OrchestrationInstanceStatus.Completed,
            TimeSpan.FromSeconds(240));
        await WaitForInvocationCountAsync(services.Scenario, reservationTopic, 3);
        await WaitForInvocationCountAsync(services.Scenario, confirmationTopic, 1);
        var attempts = await AttemptsForTaskAsync(runtime, reservationTopic);

        Assert.Equal(3, attempts.Length);
        Assert.Equal([1, 2, 3], attempts.Select(attempt => attempt.AttemptNumber).ToArray());
        Assert.Equal(TaskExecutionStatus.Failed, attempts[0].Status);
        Assert.Equal(TaskExecutionStatus.Failed, attempts[1].Status);
        Assert.Equal(TaskExecutionStatus.Completed, attempts[2].Status);
    }

    [Fact(Timeout = 180_000)]
    public async Task RuntimeCompensatesCompletedTasksWhenRetryBudgetEndsWithPermanentFailure()
    {
        var suffix = NewSuffix();
        var version = new SemanticVersion(2, 2, 0);
        var triggerTopic = Topic(suffix, "events.payment.compensation.requested");
        var inventoryTopic = Topic(suffix, "commands.inventories.stock.reserve");
        var releaseInventoryTopic = Topic(suffix, "commands.inventories.stock.release");
        var paymentTopic = Topic(suffix, "commands.payments.payment.capture");
        var orchestrationKey = Topic(suffix, "payment.compensation");

        await using var services = await RealMessagingServiceHost.StartAsync(
            _infrastructure,
            Endpoints(version, inventoryTopic, releaseInventoryTopic, paymentTopic));
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure);

        services.Scenario.Enqueue(inventoryTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("reserved", true))));
        services.Scenario.Enqueue(
            paymentTopic,
            RealMessagingServiceOutcome.Failure(
                "PaymentTemporaryFailure",
                "Payment provider throttled the first attempt.",
                isRetryableCandidate: true));
        services.Scenario.Enqueue(
            paymentTopic,
            RealMessagingServiceOutcome.Failure(
                "PaymentRejected",
                "Payment was rejected.",
                isRetryableCandidate: false));
        services.Scenario.Enqueue(releaseInventoryTopic, RealMessagingServiceOutcome.NoReply());

        var artifact = Artifact(
            orchestrationKey,
            version,
            $"real-compensation-{suffix}",
            [EventTrigger(triggerTopic, version)],
            Stage(
                "reservation",
                1,
                MessagingTask(
                    inventoryTopic,
                    1,
                    version,
                    compensation: Compensation(releaseInventoryTopic, version))),
            Stage(
                "payment",
                2,
                MessagingTask(
                    paymentTopic,
                    1,
                    version,
                    retryPolicy: RetryPolicy(1, "PaymentTemporaryFailure"),
                    onErrorPolicy: OnErrorPolicy.StopAndCompensate)));

        await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        var correlationId = $"corr-{suffix}-compensation";
        await runtime.PublishTriggerAsync(
            triggerTopic,
            version.ToString(),
            BusinessPayload(("saleId", $"S-{suffix}")),
            correlationId);

        var instance = await runtime.WaitForInstanceStatusAsync(
            correlationId,
            OrchestrationInstanceStatus.Compensated,
            TimeSpan.FromSeconds(90));
        await WaitForInvocationCountAsync(services.Scenario, releaseInventoryTopic, 1);
        var paymentAttempts = await AttemptsForTaskAsync(runtime, paymentTopic);
        var compensations = await runtime.QueryAsync(dbContext => dbContext.CompensationExecutions
            .AsNoTracking()
            .Where(compensation => compensation.OrchestrationInstanceId == instance.Id)
            .ToArrayAsync());

        Assert.Equal(2, paymentAttempts.Length);
        Assert.Contains(paymentAttempts, attempt => attempt.ErrorCode == "PaymentTemporaryFailure");
        Assert.Contains(paymentAttempts, attempt => attempt.ErrorCode == "PaymentRejected");
        Assert.Single(compensations);
        Assert.NotEqual("Failed", compensations[0].Status);
    }

    [Fact(Timeout = 240_000)]
    public async Task RuntimeCompensatesCompletedTasksAcrossMultipleStagesWhenLateFailureStopsSaga()
    {
        var suffix = NewSuffix();
        var version = new SemanticVersion(2, 7, 0);
        var triggerTopic = Topic(suffix, "events.compensation.deep.requested");
        var inventoryTopic = Topic(suffix, "commands.inventories.compensation.deep.reserve");
        var releaseInventoryTopic = Topic(suffix, "commands.inventories.compensation.deep.release");
        var paymentTopic = Topic(suffix, "commands.payments.compensation.deep.capture");
        var refundPaymentTopic = Topic(suffix, "commands.payments.compensation.deep.refund");
        var shipmentTopic = Topic(suffix, "commands.shipping.compensation.deep.book");
        var cancelShipmentTopic = Topic(suffix, "commands.shipping.compensation.deep.cancel");
        var settlementTopic = Topic(suffix, "commands.settlement.compensation.deep.commit");
        var orchestrationKey = Topic(suffix, "compensation.deep");

        await using var services = await RealMessagingServiceHost.StartAsync(
            _infrastructure,
            Endpoints(
                version,
                inventoryTopic,
                releaseInventoryTopic,
                paymentTopic,
                refundPaymentTopic,
                shipmentTopic,
                cancelShipmentTopic,
                settlementTopic));
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure);

        services.Scenario.Enqueue(inventoryTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("reserved", true))));
        services.Scenario.Enqueue(paymentTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("captured", true))));
        services.Scenario.Enqueue(shipmentTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("booked", true))));
        services.Scenario.Enqueue(
            settlementTopic,
            RealMessagingServiceOutcome.Failure("PermanentFailure", "Settlement rejected.", isRetryableCandidate: false));
        services.Scenario.Enqueue(cancelShipmentTopic, RealMessagingServiceOutcome.NoReply());
        services.Scenario.Enqueue(refundPaymentTopic, RealMessagingServiceOutcome.NoReply());
        services.Scenario.Enqueue(releaseInventoryTopic, RealMessagingServiceOutcome.NoReply());

        var artifact = Artifact(
            orchestrationKey,
            version,
            $"real-deep-compensation-{suffix}",
            [EventTrigger(triggerTopic, version)],
            Stage(
                "reservation",
                1,
                MessagingTask(
                    inventoryTopic,
                    1,
                    version,
                    compensation: Compensation(releaseInventoryTopic, version))),
            Stage(
                "payment",
                2,
                MessagingTask(
                    paymentTopic,
                    1,
                    version,
                    compensation: Compensation(refundPaymentTopic, version))),
            Stage(
                "shipment",
                3,
                MessagingTask(
                    shipmentTopic,
                    1,
                    version,
                    compensation: Compensation(cancelShipmentTopic, version))),
            Stage(
                "settlement",
                4,
                MessagingTask(
                    settlementTopic,
                    1,
                    version,
                    onErrorPolicy: OnErrorPolicy.StopAndCompensate)));

        await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        var correlationId = $"corr-{suffix}-deep-compensation";
        await runtime.PublishTriggerAsync(
            triggerTopic,
            version.ToString(),
            BusinessPayload(("saleId", $"S-{suffix}")),
            correlationId);

        var instance = await runtime.WaitForInstanceStatusAsync(
            correlationId,
            OrchestrationInstanceStatus.Compensated,
            TimeSpan.FromSeconds(120));
        await WaitForInvocationCountAsync(services.Scenario, releaseInventoryTopic, 1);
        await WaitForInvocationCountAsync(services.Scenario, refundPaymentTopic, 1);
        await WaitForInvocationCountAsync(services.Scenario, cancelShipmentTopic, 1);
        var compensations = await runtime.QueryAsync(dbContext => dbContext.CompensationExecutions
            .AsNoTracking()
            .Where(compensation => compensation.OrchestrationInstanceId == instance.Id)
            .ToArrayAsync());
        var settlementAttempts = await AttemptsForTaskAsync(runtime, settlementTopic);

        Assert.Equal(3, compensations.Length);
        Assert.All(compensations, compensation => Assert.Equal("Completed", compensation.Status));
        Assert.Single(settlementAttempts);
        Assert.Equal("PermanentFailure", settlementAttempts[0].ErrorCode);
    }

    [Fact(Timeout = 180_000)]
    public async Task RuntimeReconcilesTimedOutServiceAndCompletesWhenRetryReplyArrives()
    {
        var suffix = NewSuffix();
        var version = new SemanticVersion(2, 3, 0);
        var triggerTopic = Topic(suffix, "events.risk.timeout.requested");
        var riskTopic = Topic(suffix, "commands.risk.sale.evaluate");
        var orchestrationKey = Topic(suffix, "risk.timeout");

        await using var services = await RealMessagingServiceHost.StartAsync(
            _infrastructure,
            Endpoints(version, riskTopic));
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure);

        services.Scenario.Enqueue(riskTopic, RealMessagingServiceOutcome.NoReply());
        services.Scenario.Enqueue(riskTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("approved", true))));

        var artifact = Artifact(
            orchestrationKey,
            version,
            $"real-timeout-{suffix}",
            [EventTrigger(triggerTopic, version)],
            Stage(
                "risk",
                1,
                MessagingTask(
                    riskTopic,
                    1,
                    version,
                    timeoutPolicy: ReconcileTimeoutPolicy(
                        Duration.FromSeconds(1),
                        RetryPolicy(1, "TaskTimedOut")))));

        await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        var correlationId = $"corr-{suffix}-timeout";
        await runtime.PublishTriggerAsync(
            triggerTopic,
            version.ToString(),
            BusinessPayload(("saleId", $"S-{suffix}")),
            correlationId);

        await runtime.WaitForInstanceStatusAsync(
            correlationId,
            OrchestrationInstanceStatus.Completed,
            TimeSpan.FromSeconds(100));
        await WaitForInvocationCountAsync(services.Scenario, riskTopic, 2);
        var attempts = await AttemptsForTaskAsync(runtime, riskTopic);

        Assert.Contains(attempts, attempt => attempt.Status == TaskExecutionStatus.TimedOut);
        Assert.Contains(attempts, attempt => attempt.Status == TaskExecutionStatus.Completed);
    }

    [Fact(Timeout = 240_000)]
    public async Task RuntimeReconcilesTimedOutMiddleStageThenContinuesRemainingStages()
    {
        var suffix = NewSuffix();
        var version = new SemanticVersion(2, 8, 0);
        var triggerTopic = Topic(suffix, "events.reconcile.middle.requested");
        var validationTopic = Topic(suffix, "commands.sales.reconcile.middle.validate");
        var riskTopic = Topic(suffix, "commands.risk.reconcile.middle.evaluate");
        var notificationTopic = Topic(suffix, "commands.notifications.reconcile.middle.send");
        var orchestrationKey = Topic(suffix, "reconcile.middle");

        await using var services = await RealMessagingServiceHost.StartAsync(
            _infrastructure,
            Endpoints(version, validationTopic, riskTopic, notificationTopic));
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure);

        services.Scenario.Enqueue(validationTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("validated", true))));
        services.Scenario.Enqueue(riskTopic, RealMessagingServiceOutcome.NoReply());
        services.Scenario.Enqueue(riskTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("riskApproved", true))));
        services.Scenario.Enqueue(notificationTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("notified", true))));

        var artifact = Artifact(
            orchestrationKey,
            version,
            $"real-middle-reconcile-{suffix}",
            [EventTrigger(triggerTopic, version)],
            Stage("validation", 1, MessagingTask(validationTopic, 1, version)),
            Stage(
                "risk",
                2,
                MessagingTask(
                    riskTopic,
                    1,
                    version,
                    timeoutPolicy: ReconcileTimeoutPolicy(
                        Duration.FromSeconds(1),
                        RetryPolicy(1, "TaskTimedOut")))),
            Stage("notification", 3, MessagingTask(notificationTopic, 1, version)));

        await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        var correlationId = $"corr-{suffix}-middle-reconcile";
        await runtime.PublishTriggerAsync(
            triggerTopic,
            version.ToString(),
            BusinessPayload(("saleId", $"S-{suffix}")),
            correlationId);

        var instance = await runtime.WaitForInstanceStatusAsync(
            correlationId,
            OrchestrationInstanceStatus.Completed,
            TimeSpan.FromSeconds(120));
        await WaitForInvocationCountAsync(services.Scenario, riskTopic, 2);
        await WaitForInvocationCountAsync(services.Scenario, notificationTopic, 1);
        var riskAttempts = await AttemptsForTaskAsync(runtime, riskTopic);
        var stages = await runtime.QueryAsync(dbContext => dbContext.StageExecutions
            .AsNoTracking()
            .Where(stage => stage.OrchestrationInstanceId == instance.Id)
            .ToArrayAsync());

        Assert.Contains(riskAttempts, attempt => attempt.Status == TaskExecutionStatus.TimedOut);
        Assert.Contains(riskAttempts, attempt => attempt.Status == TaskExecutionStatus.Completed);
        Assert.All(stages, stage => Assert.Equal(StageExecutionStatus.Completed, stage.Status));
    }

    [Fact(Timeout = 360_000)]
    public async Task RuntimeWaitsParallelGroupWhileOneTaskRetriesThenContinuesFanIn()
    {
        var suffix = NewSuffix();
        var version = new SemanticVersion(2, 9, 0);
        var triggerTopic = Topic(suffix, "events.parallel.retry.requested");
        var inventoryTopic = Topic(suffix, "commands.inventories.parallel.retry.reserve");
        var paymentTopic = Topic(suffix, "commands.payments.parallel.retry.capture");
        var riskTopic = Topic(suffix, "commands.risk.parallel.retry.evaluate");
        var notificationTopic = Topic(suffix, "commands.notifications.parallel.retry.send");
        var orchestrationKey = Topic(suffix, "parallel.retry");
        var parallelGroupId = Id.New();

        await using var services = await RealMessagingServiceHost.StartAsync(
            _infrastructure,
            Endpoints(version, inventoryTopic, paymentTopic, riskTopic, notificationTopic));
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure);

        services.Scenario.Enqueue(inventoryTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("reserved", true))));
        services.Scenario.Enqueue(
            paymentTopic,
            RealMessagingServiceOutcome.Failure("PaymentTemporaryFailure", "Payment provider throttled.", isRetryableCandidate: true));
        services.Scenario.Enqueue(paymentTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("captured", true))));
        services.Scenario.Enqueue(riskTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("approved", true))));
        services.Scenario.Enqueue(notificationTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("notified", true))));

        var artifact = Artifact(
            orchestrationKey,
            version,
            $"real-parallel-retry-{suffix}",
            [EventTrigger(triggerTopic, version)],
            StageWithGraph(
                "parallel-fulfillment",
                1,
                [new ParallelGroupArtifact(parallelGroupId, ParallelJoinPolicy.WaitAll, null)],
                [],
                MessagingTask(inventoryTopic, 1, version, executionMode: TaskExecutionMode.Parallel, parallelGroupId: parallelGroupId),
                MessagingTask(
                    paymentTopic,
                    2,
                    version,
                    retryPolicy: RetryPolicy(1, "PaymentTemporaryFailure"),
                    executionMode: TaskExecutionMode.Parallel,
                    parallelGroupId: parallelGroupId),
                MessagingTask(riskTopic, 3, version, executionMode: TaskExecutionMode.Parallel, parallelGroupId: parallelGroupId)),
            Stage("notification", 2, MessagingTask(notificationTopic, 1, version)));

        await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        var correlationId = $"corr-{suffix}-parallel-retry";
        await runtime.PublishTriggerAsync(
            triggerTopic,
            version.ToString(),
            BusinessPayload(("saleId", $"S-{suffix}")),
            correlationId);

        await runtime.WaitForInstanceStatusAsync(
            correlationId,
            OrchestrationInstanceStatus.Completed,
            TimeSpan.FromSeconds(240));
        await WaitForInvocationCountAsync(services.Scenario, paymentTopic, 2);
        await WaitForInvocationCountAsync(services.Scenario, notificationTopic, 1);
        var paymentAttempts = await AttemptsForTaskAsync(runtime, paymentTopic);
        var notificationAttempts = await AttemptsForTaskAsync(runtime, notificationTopic);

        Assert.Equal(2, paymentAttempts.Length);
        Assert.Equal(TaskExecutionStatus.Failed, paymentAttempts[0].Status);
        Assert.Equal(TaskExecutionStatus.Completed, paymentAttempts[1].Status);
        Assert.Single(notificationAttempts);
        Assert.Equal(TaskExecutionStatus.Completed, notificationAttempts[0].Status);
    }

    [Fact(Timeout = 240_000)]
    public async Task RuntimeEvaluatesBranchRulesWithButterMorphAndSkipsIntermediateStages()
    {
        var suffix = NewSuffix();
        var version = new SemanticVersion(2, 10, 0);
        var triggerTopic = Topic(suffix, "events.branch.requested");
        var validationTopic = Topic(suffix, "commands.branch.validate");
        var fraudTopic = Topic(suffix, "commands.branch.fraud.review");
        var captureTopic = Topic(suffix, "commands.branch.capture");
        var orchestrationKey = Topic(suffix, "branching");
        var validationStageId = Id.New();
        var fraudStageId = Id.New();
        var captureStageId = Id.New();

        await using var services = await RealMessagingServiceHost.StartAsync(
            _infrastructure,
            Endpoints(version, validationTopic, fraudTopic, captureTopic));
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure, butterMorphEnabled: true);

        services.Scenario.Enqueue(validationTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("validated", true))));
        services.Scenario.Enqueue(captureTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("captured", true))));

        var artifact = Artifact(
            orchestrationKey,
            version,
            $"real-branch-{suffix}",
            [EventTrigger(triggerTopic, version)],
            StageWithGraph(
                validationStageId,
                "validation",
                1,
                [],
                [BranchRule(validationStageId, captureStageId, "$trigger.skipFraud")],
                MessagingTask("sales.validate", 1, version, topic: validationTopic)),
            StageWithGraph(
                fraudStageId,
                "fraud-review",
                2,
                [],
                [],
                MessagingTask("fraud.review", 1, version, topic: fraudTopic)),
            StageWithGraph(
                captureStageId,
                "capture",
                3,
                [],
                [],
                MessagingTask("payment.capture", 1, version, topic: captureTopic)));

        await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        var correlationId = $"corr-{suffix}-branch";
        await runtime.PublishTriggerAsync(
            triggerTopic,
            version.ToString(),
            BusinessPayload(("saleId", $"S-{suffix}"), ("skipFraud", true)),
            correlationId);

        var instance = await runtime.WaitForInstanceStatusAsync(
            correlationId,
            OrchestrationInstanceStatus.Completed,
            TimeSpan.FromSeconds(120));
        var stages = await runtime.QueryAsync(dbContext => dbContext.StageExecutions
            .AsNoTracking()
            .Where(stage => stage.OrchestrationInstanceId == instance.Id)
            .OrderBy(stage => stage.Order)
            .ToArrayAsync());

        Assert.Equal(0, services.Scenario.CountInvocations(fraudTopic));
        Assert.Contains(stages, stage => stage.StageKey == "fraud-review" && stage.Status == StageExecutionStatus.Skipped);
        Assert.Contains(stages, stage => stage.StageKey == "capture" && stage.Status == StageExecutionStatus.Completed);
    }

    [Fact(Timeout = 240_000)]
    public async Task RuntimeAppliesButterMorphConditionsAndTransformsAgainstAccumulatedPayload()
    {
        var suffix = NewSuffix();
        var version = new SemanticVersion(2, 11, 0);
        var triggerTopic = Topic(suffix, "events.transform.requested");
        var reserveTopic = Topic(suffix, "commands.transform.inventory.reserve");
        var loyaltyTopic = Topic(suffix, "commands.transform.loyalty.accrue");
        var captureTopic = Topic(suffix, "commands.transform.payment.capture");
        var orchestrationKey = Topic(suffix, "transform.condition");

        await using var services = await RealMessagingServiceHost.StartAsync(
            _infrastructure,
            Endpoints(version, reserveTopic, loyaltyTopic, captureTopic));
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure, butterMorphEnabled: true);

        services.Scenario.Enqueue(reserveTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("reservationId", $"R-{suffix}"))));
        services.Scenario.Enqueue(captureTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("captured", true))));

        var artifact = Artifact(
            orchestrationKey,
            version,
            $"real-transform-{suffix}",
            [EventTrigger(triggerTopic, version)],
            Stage("reservation", 1, MessagingTask("inventory.reserve", 1, version, topic: reserveTopic)),
            Stage(
                "payment",
                2,
                MessagingTask(
                    "loyalty.accrue",
                    1,
                    version,
                    topic: loyaltyTopic,
                    executionCondition: Condition("$trigger.applyLoyalty")),
                MessagingTask(
                    "payment.capture",
                    2,
                    version,
                    topic: captureTopic,
                    transformation: Transformation(
                        """
                        target {
                          saleId: $trigger.saleId
                          amount: $trigger.amount
                          reservationId: $responses.reservation.inventory_reserve.reservationId
                        }
                        """))));

        await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        var correlationId = $"corr-{suffix}-transform";
        await runtime.PublishTriggerAsync(
            triggerTopic,
            version.ToString(),
            BusinessPayload(("saleId", $"S-{suffix}"), ("amount", 1550), ("applyLoyalty", false)),
            correlationId);

        var instance = await runtime.WaitForInstanceStatusAsync(
            correlationId,
            OrchestrationInstanceStatus.Completed,
            TimeSpan.FromSeconds(120));
        await WaitForInvocationCountAsync(services.Scenario, captureTopic, 1);
        var captureInvocation = services.Scenario.Invocations.Single(invocation =>
            string.Equals(invocation.Topic, captureTopic, StringComparison.OrdinalIgnoreCase));
        var skippedTasks = await runtime.QueryAsync(dbContext => dbContext.TaskExecutions
            .AsNoTracking()
            .Where(task => task.OrchestrationInstanceId == instance.Id && task.WasSkipped)
            .ToArrayAsync());

        Assert.Equal(0, services.Scenario.CountInvocations(loyaltyTopic));
        Assert.Contains(skippedTasks, task => task.TaskKey == "loyalty.accrue");
        Assert.Equal($"S-{suffix}", captureInvocation.Payload!["saleId"]?.GetValue<string>());
        Assert.Equal($"R-{suffix}", captureInvocation.Payload!["reservationId"]?.GetValue<string>());
        Assert.Equal(1550, captureInvocation.Payload!["amount"]?.GetValue<int>());
        AssertBusinessPayloadWasNotWrapped(captureInvocation.Payload);
    }

    [Fact(Timeout = 240_000)]
    public async Task RuntimeIgnoresDuplicateAndLateCallbacksThroughRealBackchannel()
    {
        var suffix = NewSuffix();
        var version = new SemanticVersion(2, 12, 0);
        var triggerTopic = Topic(suffix, "events.late.callback.requested");
        var reserveTopic = Topic(suffix, "commands.late.callback.reserve");
        var orchestrationKey = Topic(suffix, "late.callback");

        await using var services = await RealMessagingServiceHost.StartAsync(
            _infrastructure,
            Endpoints(version, reserveTopic));
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure);

        services.Scenario.Enqueue(
            reserveTopic,
            RealMessagingServiceOutcome.Failure("TransientFailure", "First attempt failed.", isRetryableCandidate: true));
        services.Scenario.Enqueue(reserveTopic, RealMessagingServiceOutcome.Success(BusinessPayload(("reserved", true), ("attempt", 2))));

        var artifact = Artifact(
            orchestrationKey,
            version,
            $"real-late-callback-{suffix}",
            [EventTrigger(triggerTopic, version)],
            Stage(
                "reservation",
                1,
                MessagingTask(
                    "inventory.reserve",
                    1,
                    version,
                    topic: reserveTopic,
                    retryPolicy: RetryPolicy(1, "TransientFailure"))));

        await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        var correlationId = $"corr-{suffix}-late-callback";
        await runtime.PublishTriggerAsync(
            triggerTopic,
            version.ToString(),
            BusinessPayload(("saleId", $"S-{suffix}")),
            correlationId);

        await runtime.WaitForInstanceStatusAsync(
            correlationId,
            OrchestrationInstanceStatus.Completed,
            TimeSpan.FromSeconds(120));
        await WaitForInvocationCountAsync(services.Scenario, reserveTopic, 2);
        var firstInvocation = services.Scenario.Invocations
            .Where(invocation => string.Equals(invocation.Topic, reserveTopic, StringComparison.OrdinalIgnoreCase))
            .OrderBy(invocation => invocation.MessageMetadata.Attempt)
            .First();

        await runtime.PublishBackchannelAsync(
            firstInvocation.MessageMetadata.ReplyAddress!,
            firstInvocation.MessageMetadata,
            new OrchestrationExecutionResultMetadata
            {
                Succeeded = true,
                Status = "Succeeded",
                CompletedOnUtc = DateTime.UtcNow
            },
            BusinessPayload(("reserved", false), ("late", true)));

        await Task.Delay(TimeSpan.FromSeconds(2));
        var attempts = await AttemptsForTaskAsync(runtime, "inventory.reserve");
        var completedAttempt = attempts.Single(attempt => attempt.Status == TaskExecutionStatus.Completed);

        Assert.Equal(2, attempts.Length);
        Assert.Equal(2, completedAttempt.AttemptNumber);
        Assert.True(completedAttempt.ResponsePayload!["reserved"]!.GetValue<bool>());
        Assert.Null(completedAttempt.ResponsePayload!["late"]);
    }

    [Fact(Timeout = 240_000)]
    public async Task RuntimeDeploysNewArtifactVersionWhileExistingInstanceIsInFlight()
    {
        var suffix = NewSuffix();
        var orchestrationKey = Topic(suffix, "redeploy.in.flight");
        var versionOne = new SemanticVersion(2, 13, 0);
        var versionTwo = new SemanticVersion(2, 13, 1);
        var triggerOne = Topic(suffix, "events.redeploy.v1");
        var triggerTwo = Topic(suffix, "events.redeploy.v2");
        var delayedTopic = Topic(suffix, "commands.redeploy.delayed");
        var v2Topic = Topic(suffix, "commands.redeploy.v2");

        await using var services = await RealMessagingServiceHost.StartAsync(
            _infrastructure,
            [
                new RealMessagingConsumerEndpoint(delayedTopic, versionOne.ToString()),
                new RealMessagingConsumerEndpoint(v2Topic, versionTwo.ToString())
            ]);
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure);

        services.Scenario.Enqueue(
            delayedTopic,
            RealMessagingServiceOutcome.DelayedSuccess(BusinessPayload(("version", "v1")), TimeSpan.FromSeconds(2)));
        services.Scenario.Enqueue(v2Topic, RealMessagingServiceOutcome.Success(BusinessPayload(("version", "v2"))));

        var artifactOne = Artifact(
            orchestrationKey,
            versionOne,
            $"real-redeploy-v1-{suffix}",
            [EventTrigger(triggerOne, versionOne)],
            Stage("v1", 1, MessagingTask("redeploy.v1", 1, versionOne, topic: delayedTopic)));

        var artifactTwo = Artifact(
            orchestrationKey,
            versionTwo,
            $"real-redeploy-v2-{suffix}",
            [EventTrigger(triggerTwo, versionTwo)],
            Stage("v2", 1, MessagingTask("redeploy.v2", 1, versionTwo, topic: v2Topic)));

        var deploymentOne = await runtime.DeployAndWaitReadyAsync(CreatePackage(artifactOne));
        var correlationOne = $"corr-{suffix}-redeploy-v1";
        await runtime.PublishTriggerAsync(triggerOne, versionOne.ToString(), BusinessPayload(("saleId", "S-V1")), correlationOne);
        await WaitForInvocationCountAsync(services.Scenario, delayedTopic, 1);

        var deploymentTwo = await runtime.DeployAndWaitReadyAsync(CreatePackage(artifactTwo));
        var correlationTwo = $"corr-{suffix}-redeploy-v2";
        await runtime.PublishTriggerAsync(triggerTwo, versionTwo.ToString(), BusinessPayload(("saleId", "S-V2")), correlationTwo);

        var instanceTwo = await runtime.WaitForInstanceStatusAsync(correlationTwo, OrchestrationInstanceStatus.Completed);
        var instanceOne = await runtime.WaitForInstanceStatusAsync(
            correlationOne,
            OrchestrationInstanceStatus.Completed,
            TimeSpan.FromSeconds(120));

        Assert.Equal(deploymentOne.RuntimeArtifactId, instanceOne.RuntimeOrchestrationArtifactId.ToString());
        Assert.Equal(deploymentTwo.RuntimeArtifactId, instanceTwo.RuntimeOrchestrationArtifactId.ToString());
    }

    [Fact(Timeout = 240_000)]
    public async Task RuntimeProcessesManyConcurrentTriggerInstancesThroughRealMessaging()
    {
        var suffix = NewSuffix();
        var version = new SemanticVersion(2, 14, 0);
        var triggerTopic = Topic(suffix, "events.concurrent.requested");
        var taskTopic = Topic(suffix, "commands.concurrent.process");
        var orchestrationKey = Topic(suffix, "concurrent");
        const int instanceCount = 20;

        await using var services = await RealMessagingServiceHost.StartAsync(
            _infrastructure,
            Endpoints(version, taskTopic));
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure);

        var artifact = Artifact(
            orchestrationKey,
            version,
            $"real-concurrent-{suffix}",
            [EventTrigger(triggerTopic, version)],
            Stage("process", 1, MessagingTask("concurrent.process", 1, version, topic: taskTopic)));

        await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        var correlations = Enumerable.Range(0, instanceCount)
            .Select(index => $"corr-{suffix}-concurrent-{index}")
            .ToArray();

        await Task.WhenAll(correlations.Select((correlation, index) =>
            runtime.PublishTriggerAsync(
                triggerTopic,
                version.ToString(),
                BusinessPayload(("saleId", $"S-{suffix}-{index}")),
                correlation)));

        foreach (var correlation in correlations)
        {
            await runtime.WaitForInstanceStatusAsync(
                correlation,
                OrchestrationInstanceStatus.Completed,
                TimeSpan.FromSeconds(120));
        }

        await WaitForInvocationCountAsync(services.Scenario, taskTopic, instanceCount);
        var completedCount = await runtime.QueryAsync(dbContext => dbContext.OrchestrationInstances
            .AsNoTracking()
            .CountAsync(instance =>
                correlations.Contains(instance.CorrelationId) &&
                instance.Status == OrchestrationInstanceStatus.Completed));

        Assert.Equal(instanceCount, completedCount);
    }

    [Fact(Timeout = 180_000)]
    public async Task RuntimeKeepsConcurrentMessagingArtifactVersionsAvailable()
    {
        var suffix = NewSuffix();
        var orchestrationKey = Topic(suffix, "sales.versioned");
        var versionOne = new SemanticVersion(1, 0, 0);
        var versionTwo = new SemanticVersion(1, 1, 0);
        var triggerOne = Topic(suffix, "events.sales.versioned.v1");
        var triggerTwo = Topic(suffix, "events.sales.versioned.v1_1");
        var taskOne = Topic(suffix, "commands.sales.versioned.v1");
        var taskTwo = Topic(suffix, "commands.sales.versioned.v1_1");

        await using var services = await RealMessagingServiceHost.StartAsync(
            _infrastructure,
            [
                new RealMessagingConsumerEndpoint(taskOne, versionOne.ToString()),
                new RealMessagingConsumerEndpoint(taskTwo, versionTwo.ToString())
            ]);
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(_infrastructure);

        var artifactOne = Artifact(
            orchestrationKey,
            versionOne,
            $"real-version-one-{suffix}",
            [EventTrigger(triggerOne, versionOne)],
            Stage("version-one", 1, MessagingTask(taskOne, 1, versionOne)));
        var artifactTwo = Artifact(
            orchestrationKey,
            versionTwo,
            $"real-version-two-{suffix}",
            [EventTrigger(triggerTwo, versionTwo)],
            Stage("version-two", 1, MessagingTask(taskTwo, 1, versionTwo)));

        var deploymentOne = await runtime.DeployAndWaitReadyAsync(CreatePackage(artifactOne));
        var deploymentTwo = await runtime.DeployAndWaitReadyAsync(CreatePackage(artifactTwo));

        var correlationOne = $"corr-{suffix}-v1";
        var correlationTwo = $"corr-{suffix}-v11";
        await runtime.PublishTriggerAsync(triggerOne, versionOne.ToString(), BusinessPayload(("saleId", "S-V1")), correlationOne);
        await runtime.PublishTriggerAsync(triggerTwo, versionTwo.ToString(), BusinessPayload(("saleId", "S-V11")), correlationTwo);

        var instanceOne = await runtime.WaitForInstanceStatusAsync(correlationOne, OrchestrationInstanceStatus.Completed);
        var instanceTwo = await runtime.WaitForInstanceStatusAsync(correlationTwo, OrchestrationInstanceStatus.Completed);

        Assert.Equal(deploymentOne.RuntimeArtifactId, instanceOne.RuntimeOrchestrationArtifactId.ToString());
        Assert.Equal(deploymentTwo.RuntimeArtifactId, instanceTwo.RuntimeOrchestrationArtifactId.ToString());
        Assert.NotEqual(instanceOne.RuntimeOrchestrationArtifactId, instanceTwo.RuntimeOrchestrationArtifactId);
    }

    [Fact(Timeout = 210_000)]
    public async Task RuntimeGossipMakesNewArtifactStandUpOnMultipleReplicas()
    {
        var suffix = NewSuffix();
        var databaseName = _infrastructure.CreateDatabaseName();
        var version = new SemanticVersion(2, 4, 0);
        var triggerTopic = Topic(suffix, "events.multi.replica.requested");
        var taskTopic = Topic(suffix, "commands.multi.replica.process");
        var orchestrationKey = Topic(suffix, "multi.replica");

        await using var services = await RealMessagingServiceHost.StartAsync(
            _infrastructure,
            Endpoints(version, taskTopic));
        await using var replicaOne = await RealMessagingRuntimeHarness.StartAsync(
            _infrastructure,
            databaseName,
            "replica-one",
            gossipEnabled: true);
        await using var replicaTwo = await RealMessagingRuntimeHarness.StartAsync(
            _infrastructure,
            databaseName,
            "replica-two",
            gossipEnabled: true);

        var artifact = Artifact(
            orchestrationKey,
            version,
            $"real-gossip-{suffix}",
            [EventTrigger(triggerTopic, version)],
            Stage("single", 1, MessagingTask(taskTopic, 1, version)));

        var deployment = await replicaOne.DeployAndWaitReadyAsync(CreatePackage(artifact));
        await replicaTwo.WaitForArtifactReadyAsync(deployment.RuntimeArtifactId);

        var correlationId = $"corr-{suffix}-gossip";
        await replicaTwo.PublishTriggerAsync(
            triggerTopic,
            version.ToString(),
            BusinessPayload(("saleId", $"S-{suffix}")),
            correlationId);

        await replicaOne.WaitForInstanceStatusAsync(correlationId, OrchestrationInstanceStatus.Completed);
        await WaitForInvocationCountAsync(services.Scenario, taskTopic, 1);
    }

    private static RealMessagingConsumerEndpoint[] Endpoints(
        SemanticVersion version,
        params string[] topics)
        => topics.Select(topic => new RealMessagingConsumerEndpoint(topic, version.ToString())).ToArray();

    private static string NewSuffix()
        => Guid.NewGuid().ToString("N")[..10];

    private static string Topic(string suffix, string name)
        => $"e2e.{suffix}.{name}";

    private static Task<IReadOnlyCollection<RealMessagingServiceInvocation>> WaitForInvocationCountAsync(
        RealMessagingScenario scenario,
        int expectedCount)
        => PollingAssert.EventuallyAsync(
            () => Task.FromResult(scenario.Invocations),
            invocations => invocations.Count >= expectedCount,
            $"Expected at least {expectedCount} service invocations");

    private static Task<IReadOnlyCollection<RealMessagingServiceInvocation>> WaitForInvocationCountAsync(
        RealMessagingScenario scenario,
        string topic,
        int expectedCount)
        => PollingAssert.EventuallyAsync(
            () => Task.FromResult(scenario.Invocations),
            invocations => invocations.Count(invocation => string.Equals(invocation.Topic, topic, StringComparison.OrdinalIgnoreCase)) >= expectedCount,
            $"Expected at least {expectedCount} service invocations for topic '{topic}'");

    private static Task<TaskExecutionAttempt[]> AttemptsForTaskAsync(
        RealMessagingRuntimeHarness runtime,
        string taskKey)
        => runtime.QueryAsync(async dbContext =>
        {
            var query =
                from attempt in dbContext.TaskExecutionAttempts.AsNoTracking()
                join task in dbContext.TaskExecutions.AsNoTracking()
                    on attempt.TaskExecutionId equals task.Id
                where task.TaskKey == taskKey
                orderby attempt.AttemptNumber
                select attempt;

            return await query.ToArrayAsync();
        });

    private static void AssertBusinessPayloadWasNotWrapped(System.Text.Json.Nodes.JsonNode? payload)
    {
        Assert.NotNull(payload);
        var json = payload!.AsObject();
        Assert.False(json.ContainsKey(nameof(OrchestrationExecutionResultMetadata.Succeeded)));
        Assert.False(json.ContainsKey(nameof(OrchestrationExecutionResultMetadata.Status)));
        Assert.False(json.ContainsKey(nameof(OrchestrationExecutionResultMetadata.Metadata)));
    }
}
