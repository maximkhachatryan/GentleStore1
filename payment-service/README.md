# PaymentService

A standalone payments microservice for GentleStore. It owns everything about taking money —
gateway integrations, payment state, refunds, webhooks, reconciliation — so the shop never has to.

**Integrated:** [Paynet](https://paynet.am) — ARCA cards, Idram and Telcell. Adding another
provider means writing one class against `IPaymentGateway` and registering it; nothing in the
orchestrator, the API or GentleStore changes. See [Adding a gateway](#adding-a-gateway).

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

In Development a **sandbox gateway** is also enabled — a fake, not a provider. It is now only
useful for exercising flows Paynet does not support (manual capture, cancellation); delete
`Gateways/Sandbox/` and `SandboxController` if you do not want it.

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

Wired up. A super admin switches payments on per tenant (**Admin → Stores → ⋯ → Payments**), which
enrols that store's own account here via `PUT /api/merchants/{storeId}/gateway-accounts`. From then
on its orders land as `AwaitingPayment` and are released once a payment is captured. See the
[root README](../README.md#payments) for the tenant-facing behaviour.

### The sequence

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

## Paynet

[`Infrastructure/Gateways/Paynet/`](src/PaymentService.Infrastructure/Gateways/Paynet/) —
one aggregator in front of every Armenian processor.

### Three gateway keys, one API key

Paynet requires a `processor` slug on every payment, so each is registered as its own gateway:

| Key | Method |
|---|---|
| `paynet-arca` | ARCA / bank card |
| `paynet-idram` | Idram |
| `paynet-telcell` | Telcell |

To a customer these are different ways to pay, and a shop offers several at once — so making the
choice a `gatewayKey` means the storefront picks one exactly as it would pick between two
providers, and the routing, capability and account machinery all work unchanged. The merchant
enrols each key, all pointing at the same `credentialRef`.

Credentials are looked up under the exact gateway key first, then the provider family before the
hyphen — so one `Gateways:paynet:Credentials:<ref>` block serves all three rather than being
copied per method.

### What Paynet can and cannot do

Declared capabilities: `Refunds`, `PartialRefunds`, `StatusPolling`, `Webhooks`, `HostedRedirect`.

**No `ManualCapture` and no `Cancellation`** — Paynet settles at checkout and has no void. The
orchestrator checks capabilities before calling, so `captureMode: "Manual"` is refused with
`409 not_supported` rather than failing at the gateway. An unpaid payment expires on its own; a
paid one is undone with a refund.

### AMD amounts

ISO 4217 gives AMD two decimals (the luma), so `Money` stores 5 000 ֏ as 500 000 minor units.
Paynet takes **whole drams**. The adapter divides on the way out and multiplies on the way back,
and refuses a sub-dram amount rather than rounding on the merchant's behalf:

```
5000.00 AMD -> 500000 minor -> Paynet amount 5000   ✓
5000.50 AMD -> 500050 minor -> not a whole dram     -> rejected
```

This lives in [`PaynetAmounts`](src/PaymentService.Infrastructure/Gateways/Paynet/PaynetAmounts.cs)
rather than by declaring AMD zero-decimal in `Currencies`: that class is shared by every
integration, and quietly disagreeing with ISO 4217 there would be a trap for the next one.

### Status mapping

| Paynet | Ours |
|---|---|
| `pending` | `RequiresAction` — Paynet returns a checkout URL immediately, so it is waiting on the customer |
| `processing` | `Processing` |
| `completed` | `Captured` |
| `failed` | `Failed` |
| `expired` | `Expired` |
| `refunded` | `Refunded` |
| *anything else* | `Processing`, and logged |

An unrecognised status maps to "still working on it" because the transition rules refuse a move
backwards from a terminal status — so a status Paynet adds later can never un-pay a captured
payment, and the reconciler keeps polling until it means something.

### Webhooks

`POST /api/webhooks/paynet-{processor}`, verified against
`X-Paynet-Signature: sha256=<hex>` — HMAC-SHA256 over the **raw body** with the account's
`WebhookSecret`, compared in constant time across every candidate account.

Paynet is told where to post via `Payments:PublicBaseUrl`. **Leave it unset and no
`callback_url` is sent at all** — payments then resolve by the reconciler polling, which works but
is slower.

Deduplication keys on `{payment uuid}:{status}`, not on `X-Paynet-Delivery`. A delivery id
identifies the *attempt*; what has to be deduplicated is the *event*. If Paynet's retries carry
fresh delivery ids, keying on them would let one status change through several times — verified:
a redelivery with a different `X-Paynet-Delivery` is answered `duplicate`, while a genuine status
change is not.

### A note on the published spec

Two fields are handled defensively because Paynet's OpenAPI document is ambiguous about them:

- **`amount`** is typed as a string on the payment resource and as a number on the webhook and
  refund objects, so both are accepted.
- **`metadata`** is described as key-value pairs but typed as an array of strings, so it is
  **never sent** — guessing the wrong shape would be a 422 on every payment. Metadata stays on our
  own record and is echoed in our own callbacks, which is where callers read it from anyway.

The webhook `transaction` object also carries no `refunded_amount`. A `refunded` webhook is
therefore read as a *full* refund (Paynet's status enum has one refunded value, and a partial
refund leaves the payment `completed`), while a `completed` webhook reports nothing about refunds
and leaves the recorded total alone.

### Test mode

Test versus live is decided by the **API key**, not the URL — there is no separate sandbox host.
A test key runs payments through Paynet's fake processor and sends webhooks with `livemode: false`.
Paynet's magic card numbers drive the outcome: `4111 1111 1111 1111` approves,
`4000 0000 0000 0002` declines, `4000 0000 0000 0069` is insufficient funds, and so on.

### Configuration

```jsonc
"Payments": { "PublicBaseUrl": "https://payments.example.com" },
"Gateways": {
  "paynet": {
    "Enabled": true,
    "BaseUrl": "https://paynet.am/api",
    "Processors": [ "arca", "idram", "telcell" ],
    "Credentials": {
      "acme": {
        "ApiKey": "sk_live_...",       // required
        "WebhookSecret": "whsec_...",  // required to accept webhooks
        "Domain": "shop.am",           // a verified domain; server-to-server calls carry no Origin
        "RedirectMode": "hosted"       // or "direct" to skip Paynet's page and go straight to the bank
      }
    }
  }
}
```

Never commit keys — inject them as `Gateways__paynet__Credentials__acme__ApiKey`.

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

The primary control is the network: the service's site in the root `Caddyfile` ships commented out,
so the edge proxy never routes to it and only the compose network can reach it. There is no per-request auth
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

- **Never run against live Paynet.** The integration was built from the published OpenAPI document
  and verified end to end against a stub implementing it exactly. Everything on our side of the
  boundary is proven; what has not been proven is that Paynet behaves as documented. Run one test
  payment with a real test key before trusting it.
- **No test project.** The stub-based verification lives outside the repo, so nothing guards these
  mappings in CI. The status table, the dram conversion and the signature check are the parts worth
  pinning down first.
- **Refunds are not exposed to merchants.** The API and the Paynet integration both support them;
  nothing in GentleStore's console calls them yet.
- **Outbox/reconciler claim rows with a time lease**, which is enough for a couple of instances but
  is not `SELECT … FOR UPDATE SKIP LOCKED`. Revisit before scaling out widely.
- **No idempotency-record or delivered-outbox sweeper.** Both tables grow until one is added.
- **No FX.** `Money` refuses to mix currencies rather than converting.
