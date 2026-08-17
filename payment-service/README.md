# PaymentService

A standalone payments microservice for GentleStore. It owns everything about taking money —
gateway integrations, payment state, refunds, webhooks, reconciliation — so the shop never has to.

> **No payment provider is integrated yet.** This is the abstraction layer and the machinery
> around it. Adding Stripe, Adyen or a local acquirer means writing one class against
> `IPaymentGateway` and registering it; nothing in the orchestrator, the API or GentleStore
> changes. See [Adding a gateway](#adding-a-gateway).

---

## Why it is a separate service

- **Its own database.** No table here joins to GentleStore's, and none there joins to these.
  `MerchantId`, `OrderId` and `CustomerId` are ids this service records but never resolves. Both
  sides migrate and deploy on their own schedule.
- **Its own blast radius.** Card credentials, gateway secrets and webhook endpoints stay in one
  process that does nothing else.
- **Its own uptime story.** A gateway outage degrades checkout; it does not take the catalogue down.

```
payment-service/
├─ PaymentService.sln
├─ Dockerfile
└─ src/
   ├─ PaymentService.Domain/          # Entities, enums, Money, the status rules
   ├─ PaymentService.Application/     # Ports (IPaymentGateway et al) + commands. No EF.
   ├─ PaymentService.Infrastructure/  # EF Core, orchestrator, registry, outbox, reconciler
   └─ PaymentService.Api/             # Controllers, idempotency filter, DI
```

Four projects rather than GentleStore's three, for one reason: gateway integrations live in
`Infrastructure`, so the port they implement has to sit in a project below it. `Application` holds
the ports and the command shapes and references nothing but `Domain`.

---

## Running it

```bash
docker compose up -d postgres payments      # from the repo root
```

Or locally against the compose Postgres:

```bash
cd payment-service/src/PaymentService.Api
dotnet run --urls http://localhost:5090
```

- API: `http://localhost:5090`
- Swagger: `http://localhost:5090/swagger`

The database (`gentlestore_payments`) is created and migrated on first run.

In Development a **sandbox gateway** is enabled — a fake, not a provider. It exists so the port has
a reference implementation and so the service can be exercised end to end before a provider is
chosen. Delete `Gateways/Sandbox/` and `SandboxController` once a real gateway lands.

<details>
<summary>Exercising it end to end with the sandbox</summary>

```bash
M=11111111-1111-1111-1111-111111111111

# 1. Enrol the merchant
curl -X PUT localhost:5090/api/merchants/$M/gateway-accounts -H 'content-type: application/json' -d '{
  "gatewayKey":"sandbox","mode":"Test","displayName":"Sandbox","credentialRef":"dev",
  "supportedCurrencies":["USD"] }'

# 2. Create a payment (note the Idempotency-Key)
curl -X POST localhost:5090/api/payments \
  -H 'content-type: application/json' -H "Idempotency-Key: $(uuidgen)" -d '{
  "merchantId":"'$M'","amount":12.34,"currency":"USD","orderNumber":"BS-0042",
  "captureMode":"Manual","returnUrl":"http://localhost:5174/bean-scene/orders" }'

# 3. Open action.url in a browser and press "Pay", then capture:
curl -X POST "localhost:5090/api/payments/<id>/capture?merchantId=$M" -H 'content-type: application/json' -d '{}'

# 4. Refund part of it
curl -X POST "localhost:5090/api/payments/<id>/refunds?merchantId=$M" \
  -H 'content-type: application/json' -d '{"amount":2.34,"reason":"Damaged item"}'
```

Sandbox test amounts: minor units ending in **51** decline, ending in **52** simulate an outage.

</details>

---

## API

| Endpoint | Purpose |
|---|---|
| `POST /api/payments` | Create a payment and register it with a gateway. Send `Idempotency-Key`. |
| `GET /api/payments/{id}?merchantId=` | Read one payment. |
| `GET /api/payments/by-reference/{ref}?merchantId=` | Lookup by the short code on receipts. |
| `GET /api/payments?merchantId=&orderId=&status=…` | List, filter, paginate. |
| `POST /api/payments/{id}/capture?merchantId=` | Take funds held by an authorization. |
| `POST /api/payments/{id}/cancel?merchantId=` | Void an authorization, or abandon a checkout. |
| `POST /api/payments/{id}/sync?merchantId=` | Re-read from the gateway. |
| `POST /api/payments/{id}/refunds?merchantId=` | Refund; omit `amount` for the full remainder. |
| `GET /api/payments/{id}/refunds?merchantId=` | Refunds for a payment. |
| `GET /api/gateways` | Registered integrations and their capabilities. |
| `GET`/`PUT`/`DELETE /api/merchants/{id}/gateway-accounts` | Which gateways a merchant is enrolled with. |
| `POST /api/webhooks/{gatewayKey}` | Gateway notifications. Public; verified by signature. |
| `GET /health` | Liveness. |

**`merchantId` is the tenant boundary.** There is no session to derive it from, so it is stated
explicitly and every query is filtered by it. If this service is ever exposed beyond an internal
network, that parameter is the first thing that has to come from an authenticated principal instead
of the caller.

**Amounts are `decimal` on the wire** (`12.34`), matching GentleStore's `Order.Total`. Responses
carry `amountMinor` as well, so a caller that prefers integers never has to know a currency's
exponent. Internally everything is minor units — see `Money`.

### The status that matters

`Captured` means the money moved and the order can be fulfilled. `Authorized` means it is held and
needs a capture. Everything else means not yet (`Pending`, `RequiresAction`, `Processing`) or not
at all (`Failed`, `Cancelled`, `Expired`). Refunds move it to `PartiallyRefunded` / `Refunded`.

A `201` from `POST /api/payments` does **not** mean paid — check `payment.status` and `action`.

### Error codes

Stable string codes, like GentleStore's public API. The status code carries the category:

| Status | Meaning |
|---|---|
| `400` | The request is wrong. |
| `402 declined` | The issuer refused. Terminal for this payment; a retry means a new one. |
| `404` | No such payment *for this merchant*. |
| `409` | Legal request, wrong state — `invalid_state`, `refund_exceeds_captured`, `idempotency_conflict`. |
| `422` | Request is fine, the merchant is not set up — `gateway_not_configured`, `gateway_misconfigured`. |
| `502` / `503` | The gateway rejected the call, or is unreachable. |
| `504 gateway_outcome_unknown` | **We do not know.** Not "not paid" — re-read the payment. |

---

## How GentleStore uses it

1. Order is placed → `POST /api/payments` with `orderId`, `orderNumber`, `amount`, `currency`,
   `returnUrl` and `callbackUrl`. Send an `Idempotency-Key`.
2. Storefront follows `action.url` if `action.kind` is not `None`.
3. Customer comes back → call `POST /api/payments/{id}/sync`. **Do not trust the query string the
   browser returns with** — it was written by the browser and can say anything. Sync asks the gateway.
4. `payment.captured` arrives on the `callbackUrl`, or the reconciler resolves it. Either way,
   confirm with `GET /api/payments/{id}` before releasing the order.

`captureMode: "Manual"` fits GentleStore's model well: orders are requests, not reservations, so a
store that confirms stock by hand can authorize at checkout and capture on confirmation.

### Callbacks

POSTed to the payment's `callbackUrl`, with `X-Payment-Event`, `X-Payment-Event-Id` and
(when `Payments:Callbacks:SigningSecret` is set) an HMAC-SHA256 signature over the body.

Delivery is **at-least-once**. Deduplicate on `X-Payment-Event-Id`, or simply treat the callback as
a nudge and re-read the payment — which is idempotent anyway.

Events: `payment.created`, `payment.requires_action`, `payment.authorized`, `payment.captured`,
`payment.failed`, `payment.cancelled`, `payment.expired`, `refund.created`, `refund.succeeded`,
`refund.failed`.

---

## Adding a gateway

1. Implement `IPaymentGateway` in `Infrastructure/Gateways/<Name>/`. Declare `Capabilities`
   honestly — the orchestrator rejects impossible requests up front based on them, which is how a
   merchant gets "this gateway cannot refund partially" instead of a raw gateway error.
2. If the gateway pushes notifications, also implement `IGatewayWebhookReader`. Verify the
   signature before parsing, and compare in constant time.
3. Register both in `DependencyInjection.AddGateways`.
4. Put secrets under `Gateways:<key>:Credentials:<ref>:<name>` and point a merchant's account at
   `<ref>`.

Rules an integration must follow:

- **Never throw for a decline or an outage.** Those are results — return `GatewayResult.Declined`
  / `.Transient`. An exception escaping mid-call is exactly the case where nobody can tell whether
  money moved.
- **Use `Unknown` honestly.** If the gateway was reached and the answer was lost, say so. It is
  never retried blind; it is resolved by fetching.
- **Report `null`, not `0`,** for amounts a response does not mention. Null means "not reported";
  zero would un-pay an order.
- **Be stateless.** One instance serves every merchant; per-merchant credentials arrive on
  `GatewayContext`.

---

## What the service does that a thin wrapper would not

These are the parts that are tedious to add later and expensive to get wrong:

- **Idempotency.** `Idempotency-Key` is claimed by a unique-index insert *before* the work starts,
  so two concurrent retries cannot both charge. Replays return the stored response.
- **An attempt log.** Every gateway call is written and committed *before* the call goes out. If
  the process dies mid-charge there is still a row saying we asked. Logs are sampled and expire;
  this is what a chargeback is answered with.
- **A webhook inbox.** Deliveries are stored, then applied. Deduplicated on the gateway's event id
  via a unique index, so a redelivery collapses instead of applying twice.
- **Status rules.** Webhooks are retried with backoff, so an "authorized" delivery routinely lands
  after "captured". `PaymentStatuses.CanTransitionTo` is what stops a late retry un-paying an order
  that has already shipped.
- **An outbox.** Callbacks are queued in the same transaction as the state change they describe,
  then delivered with exponential backoff. A payment that succeeded is worth nothing to the shop if
  the notification is lost.
- **A reconciler.** Polls in-flight payments the gateway never reported on and expires abandoned
  checkouts. This is the part that is easiest to leave out and hardest to do without.
- **Minor-unit money.** Rounding differently from the gateway is how a payment ends up a cent off
  and stops reconciling. `Money` converts once, at the boundary, and refuses to mix currencies.

---

## Configuration

| Key | Notes |
|---|---|
| `ConnectionStrings:Default` | Its own database. Do not point it at GentleStore's. |
| `Payments:Outbox:*` | Poll interval, batch size, retry ceiling, backoff. |
| `Payments:Reconciler:*` | Poll interval, when a payment counts as stale, when to abandon it. |
| `Payments:Callbacks:SigningSecret` | Signs callbacks. Unset in development. |
| `InternalAccess:Keys` | Optional shared secret (`X-Internal-Key`). Empty disables the check. |
| `Gateways:<key>:Credentials:<ref>:<name>` | Per-merchant gateway secrets. |

Secrets never live in the database. `MerchantGatewayAccount` holds a `credentialRef`, and
`IGatewayCredentialProvider` resolves it at call time — configuration today, a secret manager later
without touching any gateway.

### Access control

The primary control is the network: in `docker-compose.prod.yml` the service has no `VIRTUAL_HOST`,
so nginx never routes to it and only the compose network can reach it. There is no per-request auth
between GentleStore and PaymentService by design.

Two things sit alongside that:

- **`/api/webhooks/{gateway}` is not internal.** It has to be reachable by the gateway, so
  authenticity comes from the signature on the delivery, verified per gateway. It is exempt from
  the internal-key check because a gateway has no key to present.
- **`InternalAccess:Keys` is an optional second lock**, off by default. Worth setting in
  production: "internal" stops being true faster than anyone expects, and an unauthenticated
  endpoint that can issue refunds is a bad thing to discover has become reachable. It must be set
  before the service is given a public hostname for webhooks — see the comment in
  `docker-compose.prod.yml`.

---

## Not done yet

- **No gateway integration.** The point of this pass.
- **No GentleStore-side client.** Linking orders to payments needs a migration and `OrderStatus`
  changes there; that is its own step.
- **Outbox/reconciler claim rows with a time lease**, which is enough for a couple of instances but
  is not `SELECT … FOR UPDATE SKIP LOCKED`. Revisit before scaling out widely.
- **No idempotency-record or delivered-outbox sweeper.** Both tables grow until one is added.
- **No FX.** `Money` refuses to mix currencies rather than converting.
