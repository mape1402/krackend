# Pigeon: confirmed consumer readiness

## Problem

In Pigeon 4.2.0, registering a consumer announces a topic through a synchronous event. Broker adapters start the underlying consumer asynchronously, so the registration call can return before the broker has accepted the subscription. A caller cannot reliably distinguish a registered handler from a consumer that is actually ready, detect a startup failure, or wait for the startup operation to finish.

## Required behavior

Provide a transport-neutral asynchronous API that lets a caller register or observe a consumer endpoint and await its actual readiness. Identify the endpoint by topic and subscription, and account for the message version where relevant. Successful completion must mean that the local process has a live broker consumer for that endpoint, not merely that an in-memory handler or topology was registered.

The API must:

- Return a task that completes only after the broker adapter has successfully started consuming, or throws a meaningful exception if startup fails.
- Accept cancellation and allow the caller to impose a timeout without leaving unobserved startup failures or orphaned consumers.
- Report an unsupported capability explicitly. Never report readiness based only on handler registration or topology creation.
- Handle repeated and concurrent requests for the same endpoint consistently, including multiple message versions sharing one broker consumer.
- Expose the outcome for the current connection generation, so a previous successful startup cannot mask a later disconnection or failed restart.
- Define how readiness changes after disconnect, reconnect, consumer removal, host shutdown and cancellation, including cleanup and ownership of shared consumers.
- Preserve existing registration APIs and behavior for callers that do not opt into readiness confirmation.

For RabbitMQ, readiness must follow successful broker acknowledgement of the consumer subscription. Other transport adapters must provide an equivalent, documented guarantee or explicitly report that the capability is unavailable. Error and cancellation behavior must not depend on an `async void` event handler.

## Acceptance tests

1. A caller waiting for an endpoint does not complete merely because its handler was registered; it completes after the broker consumer has started.
2. An unavailable broker, failed subscription or invalid endpoint produces an observable error and does not report readiness.
3. Cancellation or timeout ends the wait and leaves no orphaned registrations or consumers.
4. Concurrent callers for a shared endpoint get consistent results; removing one registration does not stop a consumer still owned by another.
5. After disconnect and reconnect, a readiness result from the earlier connection is not reused for the new connection.
6. A supported adapter is covered by an integration test against a real broker, including startup failure and reconnection. An unsupported adapter reports the limitation explicitly.
7. Existing applications using the current registration API continue to work without adopting the new API.

## Scope

This capability belongs to Pigeon and its transport adapters. It must not depend on an orchestration framework, its storage model, or its deployment lifecycle.
