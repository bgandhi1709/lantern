# Spike: CloudAMQP, the RabbitMQ trigger and scale-to-zero (issue #175)

Run 2026-10-31 against the free LavinMQ instance from #174 (LavinMQ 2.10.2, single node, one vhost). Spec: #173, decision D119.

> **Status: done. Go for the cutover (#176)**, with the amendments below. No fallback implementation is kept in the repo: UAT test runs on CloudAMQP decide whether it holds up.

## Proven against the real instance

| Question | Answer | Evidence |
|---|---|---|
| Management API on the free plan? | Yes. `/api/overview`, queues, exchanges, bindings, publish and get all work for the vhost user (tags `management,policymaker`). | HTTP 200 on each |
| Definitions file import? | **No.** `POST /api/definitions/{vhost}` and `GET` (export) return 403 `access_refused` for the vhost user, which is not an administrator. | 403 on both, and on the global form |
| Declare topology without a definitions file? | Yes, entity by entity: `PUT /api/exchanges`, `PUT /api/queues`, `POST /api/bindings`. Repeating is safe (204 for an unchanged exchange or queue). Redeclaring a queue with changed arguments is refused with 400. | `apply.py` run twice |
| Dead-letter exchange, message TTL, alternate exchange | Work. A rejected message went to the delay queue, left after the TTL and returned to the work queue; a key with no binding landed in the parking queue through the alternate exchange. | queue depths and logs |
| Quorum queues | Accepted and **ignored**. Declaring `x-queue-type: quorum` returns 201, the argument is stored, but the broker reports no type. It is an ordinary queue. | `GET /api/queues/.../spike.quorum` |
| `x-delivery-limit` | Enforced on LavinMQ: with limit 2, a nacked-and-requeued message was dead-lettered to the DLX after the third delivery, and carried `x-delivery-count`. | probe run |
| Publisher confirms | Work with `RabbitMQ.Client` 7.2.2 (`publisherConfirmationsEnabled`). | probe run |
| Fan-out by key | One publish to key `act` reached both bound queues and both triggers. | logs |
| Trigger on a thrown exception | The extension does **not** requeue. It republishes the message to the queue (default exchange, routing key = queue name) with header `x-ms-rabbitmq-requeuecount` 1 to 4, immediately, so 5 attempts in about 70 ms. Then it rejects without requeue, and the dead-letter exchange takes over. Not configurable: the only options are `QueueName`, `PrefetchCount`, `DisableCertificateValidation`. | logs, `message_stats` |
| After the delay queue | The header stays at 4, so each cycle is one attempt, then a reject. Nothing in the broker caps the number of cycles. | logs |
| Retry limit | Cannot be expressed in the broker. `x-death` carries a `count` per queue and the isolated worker exposes it (and every other header, `Redelivered`, `Exchange`, `RoutingKey`) in `FunctionContext.BindingContext.BindingData`. The limit has to be counted in the thin trigger, which then publishes to the parking queue or acks and leaves the row in the ledger for the resend. | binding data log |
| Header values in the isolated worker | String header values arrive base64-encoded in `BasicProperties.Headers`. A custom header (`x-trace`) survives, so trace context can travel. | logs |
| Connections | One connection and one channel per function, per replica. Two functions held two connections (prefetch 30, manual ack). Free LavinMQ allows 40, so about 13 three-function replicas. | `/api/connections`, `/api/consumers` |
| A trigger parameter named `body` | Fails indexing ("Can't bind parameter 'body' to type 'System.String'"). Named `myQueueItem` it works. | host log |
| Storage | The trigger consumes with no `AzureWebJobsStorage`; only the host health check reports unhealthy. | local run |
| Vulnerabilities | None reported by `dotnet list package --vulnerable --include-transitive` for `RabbitMQ.Client` 7.2.2, `Microsoft.Azure.Functions.Worker.Extensions.RabbitMQ` 2.1.0, or the host extension it pulls in at build, `Microsoft.Azure.WebJobs.Extensions.RabbitMQ` 2.1.0 (which carries `RabbitMQ.Client` **6.8.1**, Newtonsoft.Json 13.0.1, System.Text.Json 4.6.0). | NuGet audit |
| Licences | `RabbitMQ.Client` Apache-2.0 OR MPL-2.0, both extensions MIT. All are permissive and fit PolyForm Noncommercial. | nuspec |
| Message counting | Per [CloudAMQP's FAQ and plans page](https://www.cloudamqp.com/plans/), a message counts when published **and** again when consumed. One action with two subscribers is 1 publish plus 2 deliveries. A failing message adds up to 4 extra publishes and deliveries from the extension's immediate republishing, plus one pair per delay cycle. | docs |
| Free LavinMQ limits | 2M messages a month, 200 queues, 20,000 queued messages, 40 connections, 200 channels per connection. | plans page |
| 28-day idle-queue deletion | The plans page lists "max idle queue time 28 days" for the free RabbitMQ plan; the LavinMQ column was not confirmed. Separately, a free instance with no publish or consume in two months is deleted after a warning email. Whether a queue with bindings but no traffic counts as idle is **not** verified. | docs only |

## Consequences for the spec (#173)

- **Topology as code (amend).** The release cannot import a definitions file. It applies the same file entity by entity through the management API (or declares over AMQP at start). The local container can still load the file. The apply is idempotent but cannot change the arguments of an existing queue.
- **Retry (amend).** "Retry and dead-letter policy live in infrastructure" holds for the 5-minute wait. The limit and the first five hot attempts do not: they are in the extension and the trigger. Plan for a trigger that reads `x-death` and parks after the limit.
- **Quota maths (amend).** Count deliveries as well as publishes, so the cost table's message counts roughly triple for two subscribers. The revisit trigger of 800,000 a month needs restating against that.
- **No quorum queues** on LavinMQ; nothing in the spec needs them.

## Scale to zero on Container Apps (proven)

Throwaway app `ca-spike-rmq` in `cae-lantern-uat` (Consumption, 0.5 vCPU, 1 GiB), the official Functions `dotnet-isolated:4-dotnet-isolated10.0` image, `minReplicas: 0`, `maxReplicas: 1`, `cooldownPeriod: 60`, `pollingInterval: 15`, two custom KEDA rules:

```yaml
type: rabbitmq
metadata: {protocol: amqp, queueName: spike.work-a, mode: QueueLength, value: "1"}
auth: [{secretRef: amqp-url, triggerParameter: host}]
```

| Step | Result |
|---|---|
| Empty queues, app just created | Scaled to 0 about 2.5 minutes after creation. |
| One message published to a key bound to both queues | Replica present 10 s later; both triggers received the message about 33 s after the publish (cold start). |
| Queues empty again | Back to 0 about 100 s after delivery (60 s cooldown plus polling). The production default cooldown is 300 s. |
| Management API needed by the rule? | **No.** `protocol: amqp` with `mode: QueueLength` reads the depth over AMQP with the same URL as the app. The management API is still needed for the release's topology apply. |
| Rules needed | One per queue (the scaler takes one `queueName`), so one per subscriber. The URL is a Container App secret, which the Bicep sources from the Key Vault secret `amqp-url`. |
| Failing message | Not run in the cloud. By construction KEDA counts the work queue only, so a message waiting in the delay queue does not hold a replica up (inference, not measured). |

## Idle-queue probe

Left on the instance on 2026-10-31: queue `spike.idle-probe`, bound to exchange `spike.actions` with key `idle`, never published to or consumed from. Check on or after 2026-11-28 whether it still exists, then delete both. If it was deleted, the 28-day rule applies to LavinMQ and to a queue with a binding.

## Cleanup

App deleted, scratch queues and exchanges deleted (the probe above is the only thing left on the broker). The private GHCR package `lantern-spike` is deleted.
