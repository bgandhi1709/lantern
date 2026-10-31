# Actions are published and subscribed on CloudAMQP

Supersedes [ADR-0004](0004-actions-through-service-bus-and-functions.md) for the transport, the dispatcher and the retry
mechanism. ADR-0003's guarantee (a hard delete is never abandoned), ADR-0004's ledger and its "no timer anywhere", and
ADR-0006's one-trace goal still hold. Evidence: `docs/research/cloudamqp-pubsub-spike.md`; decisions D119, D121, D122.

The Plan, Ask and Check operations of the Pilot must be able to subscribe to an action without anyone touching a
dispatcher, and a second consumer of one action needs Service Bus Standard (about ₹880 a month against about ₹0.05 for
Basic). So an action is now published once and delivered by the broker to every subscriber.

**Publish.** The API records the action in the ledger (the commit point, as before), then `IPublishingService`
(`RabbitMqPublishingService`) publishes the `ActionMessage` envelope (id, type, payload, trace parent) to the direct
exchange `lantern.actions` with the action's wire name (`remove-workspace`) as the routing key, with publisher confirms.
The service knows nothing of what it carries; a move back to Azure swaps this one class and the triggers.

**Subscribe.** Each subscriber has a durable queue named after the key it is bound to, and one Function in
`Lantern.Functions` (`RemoveWorkspaceFunction` and its two siblings) that holds its business logic. Because the
infrastructure already binds a queue to one event, a Function is the whole subscriber: there is no separate handler class
(D123). Its `[RabbitMQTrigger]` method calls the shared `ActionFunction<T>` base, which decodes the body, skips an action
whose ledger row is gone and completes the row after the Function's `HandleAsync(payload)` ran. Routing is the broker's job,
so the dispatcher, the handler layer, `UnknownActionException` and the type check are deleted. A new subscriber is a
Function and an entry in `infra/topology.json`; an architecture test confines the broker trigger to `*Function` classes and
broker client types to the publishing service. The cost is that a move off RabbitMQ edits the Functions that hold the
logic, not only a trigger signature.

**Topology is code.** `infra/topology.json` lists the exchanges, queues and bindings: `lantern.actions` (direct, its
alternate exchange `lantern.parking`), a queue per subscriber, and `parking`, bound to the fanout exchange
`lantern.parking`, which is also the dead-letter exchange of every work queue. An unroutable key and a message the
subscriber gave up on both end in `parking`. The vhost user on CloudAMQP cannot import a definitions file (403), so
`infra/apply-topology.sh` declares the file's entities one by one through the management API. It is idempotent, cannot
change the arguments of a queue that exists, and is the same script in the release and in the local stack. The release
runs it before the Bicep deployment; the exchange and queue names are also `bicepparam` values passed to the apps, and a
test fails if a queue is missing for an action type.

**Failure and retry.** The Functions extension retries a failed message itself, five times at once, not configurable, then
rejects it to the dead-letter exchange, which parks it. A parked action keeps its ledger row, so the API's resend on start
still finishes a delete (ADR-0003). The 5-minute delay queue, the limit counted from `x-death` and the alert on `parking`
come in #178.

**The one secret.** The broker URL holds a password, so it is the Key Vault secret `amqp-url`, referenced by both
Container Apps through a secret ref. This reverses ADR-0004's "no connection string exists in Azure" for this one
resource, on purpose. The deploy identity gets Key Vault Secrets User on that secret alone so the release can apply the
topology; it still cannot read any other secret.

**Scale to zero.** Three KEDA `rabbitmq` rules (`protocol: amqp`, `mode: QueueLength`, one per subscriber queue, the URL
from the secret) wake the Functions app from a message on any of them, and it goes back to zero when they are empty. The
spike measured about 33 s from publish to delivery on a cold start.

**Local.** `deploy/local` runs LavinMQ, the engine CloudAMQP hosts, not RabbitMQ, so local behaviour is the behaviour
the spike measured. A one-shot `broker-topology` container runs `apply-topology.sh`; the API and the Functions app wait
for it. The Service Bus emulator and its SQL Server container are gone, which makes the Docker E2E start faster and lighter.

**Cost.** CloudAMQP counts a message when it is published and again on each delivery. One action with one subscriber is 2
counted messages, with two subscribers 3, and every failed attempt adds more. The free LavinMQ plan allows 2M a month, 200
queues and 40 connections (one per function per replica), so 3,000 actions a month cost ₹0. The revisit trigger is 800,000
counted messages a month (40% of the free cap), which is about 400,000 actions with one subscriber. The free plan is shared
and not meant for production; UAT runs on it, and moving to Service Bus Standard topics, or a paid plan, stays a rewrite of
the publishing service and the triggers.

**Local and release ordering.** `deploy/local` runs LavinMQ, the same engine as UAT, with the `guest` login that guards nothing outside the stack. A `broker-topology` job runs `infra/apply-topology.sh` against it first, and the API and the Functions app start after it, because an action published before its queue exists is parked. The release does the same on UAT: the `deploy` job applies the topology before the Bicep deployment, using the deploy identity's role on the `amqp-url` secret alone.

**Trace.** The Azure SDK no longer puts trace context on the message. The envelope still carries the trace parent; making
one trace run from the request through the broker into the Function is #179, so until then the trace stops at the publish.

Considered and rejected: Service Bus Standard topics now (about ₹880 a month for no consumer beyond today's), keeping the
dispatcher (boilerplate in the way of every new subscriber), importing the topology as one definitions file (refused by the
vhost user), and declaring the topology from the apps at start (it puts infrastructure code in the API and the Functions
app).
