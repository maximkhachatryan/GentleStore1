# GentleStore

A simple **multi-tenant trading (retail catalog) platform** with a unified PostgreSQL database:

- **Global Admin Panel** — a super-admin manages every store (tenant) and internal users.
- **Tenant Backoffice** — each store's owner/staff manage their own catalog.
- **Public Storefront** — anonymous customers browse stores & products and order via phone/WhatsApp.

Built with **C# (.NET 10) + PostgreSQL** on the backend and **React + TypeScript + Vite** on the
frontend. The storefront is wired for **Capacitor**, so it can ship as a native Android/iOS app later.

---

## Architecture

```
GentleStore/
├─ docker-compose.yml                 # PostgreSQL 16 (+ optional pgAdmin)
├─ backend/
│  ├─ GentleStore.sln
│  └─ src/
│     ├─ GentleStore.Domain/          # Entities + enums
│     ├─ GentleStore.Infrastructure/  # EF Core DbContext, migrations, auth helpers, seeding
│     └─ GentleStore.Api/             # Minimal-API endpoints, JWT, DI, file uploads
├─ payment-service/                   # Payments microservice — own solution, own database
│  ├─ PaymentService.sln
│  └─ src/                            # Domain / Application / Infrastructure / Api
└─ frontend/                          # npm workspaces monorepo
   ├─ packages/shared/                # Typed API client + shared TS types
   └─ apps/
      ├─ console/                      # Admin + Backoffice (React + Ant Design)
      └─ storefront/                   # Public customer app (React + Tailwind + Capacitor)
```

**Multi-tenancy:** a single database with a `StoreId` discriminator on tenant-owned rows.
The backoffice is auto-scoped to the signed-in user's `StoreId`; the public API resolves the tenant
from the `{slug}` in the URL; the super-admin can act across all stores. A store can also have its
own domain, where the storefront finds the tenant from the host name instead (see below).

**Auth:** lightweight JWT. A `Users` table stores BCrypt password hashes, a role
(`SuperAdmin` / `StoreOwner` / `StoreStaff`) and an optional `StoreId`. The storefront is anonymous.

---

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/) (`dotnet --version` → 10.x)
- [Node.js 20+](https://nodejs.org/) (Node 24 recommended)
- [Docker](https://www.docker.com/) (for PostgreSQL)

---

## Getting started

### 1. Start PostgreSQL

```bash
docker compose up -d postgres
```

This runs PostgreSQL on **localhost:5433** (db `gentlestore`, user `gentlestore`, password
`gentlestore_dev_pw`). Optionally start pgAdmin with `docker compose --profile tools up -d`.

### 2. Run the backend API

```bash
cd backend/src/GentleStore.Api
dotnet run --urls http://localhost:5080
```

On first run in **Development** it automatically applies EF Core migrations and seeds demo data.

- API base URL: `http://localhost:5080`
- Swagger UI: `http://localhost:5080/swagger`

### 3. Run the frontends

```bash
cd frontend
npm install            # first time only (installs all workspaces)

npm run dev:console     # http://localhost:5173  (Admin + Backoffice)
npm run dev:storefront  # http://localhost:5174  (Public storefront)
```

Each app reads the API URL from its `.env` (`VITE_API_URL`, defaults to `http://localhost:5080`).

---

## Seeded demo data & credentials

| Role         | Email                        | Password    |
|--------------|------------------------------|-------------|
| Super Admin  | `admin@gentlestore.local`    | `Admin123!` |
| Store Owner  | `owner@bloom-petal.local`    | `Owner123!` |
| Store Owner  | `owner@bean-scene.local`     | `Owner123!` |

Two demo stores are seeded — **Bloom & Petal** (`/bloom-petal`) and **Bean Scene Coffee**
(`/bean-scene`) — each with categories, products, images and tags.

> These credentials and the JWT secret in `appsettings*.json` are for local development only.
> Change them before deploying.

---

## What you can do

- **Storefront** (`http://localhost:5174`): browse the store directory, open a store, filter products
  by category / tag / search, view product details, add to a cart and place an order — or tap
  **WhatsApp** / **Call** to ask the store first.
- **Console → Super Admin**: dashboard stats, create/edit/activate stores, manage users.
- **Console → Store Owner**: edit store profile (with logo upload), manage categories, products
  (with image upload & tag assignment) and tags — all scoped to their own store.
- **Console → Customers**: register customers by phone, hand each one a personal one-time invite
  link over WhatsApp, and see which devices are signed in (see below).
- **Console → Orders**: incoming orders with the customer's identity tier, quoting for
  "price on request" items, and the status flow (see below).

---

## Invite-only storefronts

A store can keep its storefront open to everyone or restrict it to invited customers
(**Store Profile → Who can see your storefront**). Invite-only stores are also hidden from the
public store directory.

### How it works

1. The store adds a customer by phone number (**Customers → New customer**).
2. **Send invite** mints a link like `https://shop.example.com/bloom-petal/welcome#i=<secret>` and
   opens WhatsApp with the message — and the link — already typed into that customer's chat box.
3. The first browser to open the link claims it and receives a long-lived `HttpOnly` cookie.
   That browser can now browse the catalogue.
4. The same link opened from **any other** browser is refused (`409 already_used`), while the
   browser that claimed it can re-open the WhatsApp message as often as it likes.

### Security properties

| Concern | How it is handled |
|---|---|
| Secret in a URL | 256-bit random token carried in the **URL fragment**, which browsers never send to a server — so it stays out of access logs, `Referer` headers and analytics. The storefront wipes it from the address bar on arrival. |
| Database leak | Only SHA-256 hashes of invite and session secrets are stored. Neither can be replayed. |
| Link sharing / forwarding | Single use, enforced by a conditional `UPDATE ... WHERE RedeemedAt IS NULL`, so simultaneous opens cannot both win. |
| Session theft via XSS | The session cookie is `HttpOnly` — page scripts cannot read it. |
| CSRF | `SameSite=Lax`. The API and storefront share a registrable domain, so the cookie flows between them but is withheld from requests originated by unrelated sites. |
| Brute force | Per-IP rate limit on the redeem endpoint (20 attempts / 5 min). |
| Unused links | Invites expire (`Storefront:InviteExpiryDays`, default 14 days) and are revoked automatically when a new one is generated. |
| Losing a device | The store can sign out a single device or block a customer outright. Blocking is reversible; revoking a device is not — that browser needs a new link. |

Cookies cannot literally live forever (browsers cap persistent cookies at ~400 days), so the
cookie is issued for the maximum window and its expiry is refreshed on every visit. A customer
who keeps shopping is never signed out.

---

## Search engines and store domains

### What crawlers see

The storefront is a client-rendered app, but crawlers do not have to run it to learn what a page is
about. On every page load the storefront's nginx asks the API for that page's head
(`GET /api/public/seo/head/{path}`) and splices it into `index.html` with an SSI include
([nginx.storefront.conf](frontend/nginx.storefront.conf)):

- `<title>` (`{Product} · {Store}`, `{Category} · {Store}`, `{Store}`), meta description and a canonical link.
- Open Graph / Twitter tags, which is also what WhatsApp and Telegram link previews read.
- schema.org JSON-LD: `Product` with its price or price range and stock, `OnlineStore` for the store page.
- `noindex` on invite-only stores and on cart, orders and invite pages.

Everyone gets the same HTML (people, search engines, chat apps), so this is not cloaking. If the API is slow or down, the
page keeps `index.html`'s default head and loads as usual. Outside nginx (the Vite dev server, the
Capacitor app) the include is an inert comment.

`/robots.txt` and `/sitemap.xml` are generated per host. The sitemap lists every public store's
store page, the categories that have something to buy, and the available products. Invite-only stores are never listed.

### A store's own domain

A super admin can give a store its own domain (**Admin → Stores → Edit → Own domain**). Its
storefront then lives at the root of that domain (`flowers.am/product/<id>`) instead of at
`example.com/flowers/product/<id>`:

- **Certificates** are obtained by Caddy on the first visit, after the API confirms a store owns
  the name. Adding a domain needs no deploy. See [deployment.md §13a](deployment.md) for the DNS steps.
- **The storefront** sees that it was opened on a host other than the platform's, asks the API which store
  that is (`GET /api/public/hosts/{host}`) and mounts that store's routes at `/`.
- **The session cookie** stays first-party: on a store's domain the app calls the API through that
  same domain (Caddy forwards `/api/public/*` and `/uploads/*`), never through the platform's API host.
- **Links the API builds** (invite links, payment return URLs, canonical URLs, the sitemap) use the
  domain. Everything goes through `StorefrontLinks.StoreBase`.
- **The platform's copy** of a public store's catalogue pages redirects to the domain. Invite-only
  stores and order pages are not redirected, because sessions belong to the domain they were made on.

---

## Orders

One checkout, one order pipeline, three identity tiers. The only thing that differs between a
private and a public storefront is **how well the store knows who is ordering** — recorded on every
order as `IdentityTier`:

| Tier | How the browser was identified | Phone trust | Checkout asks for |
|---|---|---|---|
| `Invited` | Redeemed a store-issued invite link | **Verified** — the store sent that link to this WhatsApp number and only its owner could have opened it | Nothing |
| `Returning` | Self-declared, but this browser has ordered here before | Self-declared | Nothing (prefilled) |
| `Guest` | Self-declared, first order from this browser | Unverified | Name + phone |

An invite-only store only ever produces `Invited` orders. A **public** store produces all three,
because invite links keep working on public storefronts — so a merchant can hand regulars a link and
give them the frictionless checkout while walk-ins still fill in the form.

### What an order records

Orders snapshot everything: product names, variant labels, unit prices, currency and the customer's
contact details. Editing the catalogue later never rewrites history.

- **Orders are requests, not reservations.** Availability is a yes/no flag with no stock counter, so
  nothing is held back and two customers can order the last item. The store confirms what it can
  actually fulfil.
- **`UnitPrice` is nullable**, because `Product.Price` is. An order containing a "price on request"
  item lands in `AwaitingQuote` with no total; the store fills in the prices (**Orders → Save prices
  and quote**) and it becomes `Quoted`.
- **Order numbers** are per-store and gapless (`BS-0001`), allocated under a row lock so two
  simultaneous checkouts can never share one.

Status flow: `New → Confirmed → Ready → Completed`, plus `AwaitingQuote → Quoted`, plus `Cancelled`.
Transitions are validated server-side; the console only offers the moves that will be accepted.

### Public checkout is also customer registration

A guest checkout creates a `Customer` row with `Origin = SelfRegistered` and binds the browser with
the same kind of session cookie an invite issues. Consequences:

- A public store's customer list fills up on its own — which is what makes switching to invite-only
  later viable instead of a cold start.
- Repeat guests are recognised: prefilled checkout, no retyping.
- `IX_Customers_StoreId_PhoneNormalized` means a later invite to that number lands on the **same
  record**, so history merges. Adding a customer whose number already ordered returns that record
  and the console offers **Send invite** — the upgrade from self-declared to verified.

### Order history is scoped by how you got in

| Session created by | Can read |
|---|---|
| An invite (verified binding) | Every order that customer ever placed |
| A guest checkout (self-declared) | Only the orders that browser placed |

Without that split, anyone who guessed a phone number could read a stranger's order history by
checking out under it. Verified once at the gate, so a guessed order id cannot widen access either —
`GET /orders/{id}` reads through the same scoped set as the list.

### The WhatsApp confirmation step

There is no SMS gateway and no WhatsApp Business API in this stack, so a public order cannot be
OTP-verified. Instead the storefront hands the customer a prefilled WhatsApp message containing the
order number, and the merchant confirms from their own inbox — where they already work. Orders that
never produce a message self-identify as abandoned or spam. Invited customers get the same button,
but the order is already trustworthy without it.

---

## API overview

| Group        | Base path            | Auth                         |
|--------------|----------------------|------------------------------|
| Auth         | `/api/auth`          | anonymous (`login`) / bearer |
| Admin        | `/api/admin`         | `SuperAdmin`                 |
| Backoffice   | `/api/backoffice`    | `StoreOwner` / `StoreStaff`  |
| Public       | `/api/public`        | anonymous (invite cookie for private stores) |
| Uploads      | `/api/uploads`       | any authenticated user       |

Uploaded images are stored under `backend/src/GentleStore.Api/wwwroot/uploads` and served at `/uploads/*`.
Any photo up to 30 MB is accepted and re-encoded on arrival: the upload itself is kept as a WebP of
at most 2560px, with smaller copies at `/uploads/{sm|md|lg}/{id}.webp` (320, 640 and 1600px on the
longest side) for thumbnails, product cards and the product page. Pick one with `resolveImageUrl` from
`@gentlestore/shared`. Images uploaded before sizes existed get theirs made on first request.

Storefront endpoints for a store set to invited-customers-only answer
`403 { "code": "invite_required" }` until the browser presents a valid session cookie. Two
endpoints are never gated, because the locked-out screen needs them:

| Endpoint | Purpose |
|---|---|
| `GET /api/public/stores/{slug}/access` | Store name, logo, phone, access mode, and whether this browser is in. |
| `POST /api/public/stores/{slug}/access/redeem` | Claims an invite secret and sets the session cookie. |

Ordering endpoints (both gated like the rest of the catalogue):

| Endpoint | Purpose |
|---|---|
| `POST /api/public/stores/{slug}/orders` | Places an order; on a public store the contact fields double as registration. |
| `GET /api/public/stores/{slug}/orders` | Order history, scoped by how the browser was identified. |
| `GET /api/backoffice/orders` | Store's order list with identity tiers. |
| `POST /api/backoffice/orders/{id}/status` | Move the order along the status flow. |
| `PUT /api/backoffice/orders/{id}/quote` | Price the "price on request" lines. |

Invite redemption and order placement are both rate limited per IP.

---

## Turning the storefront into a mobile app

The storefront is already configured for [Capacitor](https://capacitorjs.com/)
(`frontend/apps/storefront/capacitor.config.ts`, `appId: com.gentlestore.app`).

```bash
cd frontend/apps/storefront
npm run build            # produces dist/
npm run cap:add:android  # requires Android Studio  → creates android/
npm run cap:add:ios      # requires Xcode (macOS)   → creates ios/
npm run cap:sync         # copy the web build into the native projects
```

> Building native binaries requires Android Studio and/or Xcode, which are not part of this repo.
> Point the app at a deployed API by setting `VITE_API_URL` before `npm run build`.

---

## Tech stack

- **Backend:** ASP.NET Core (.NET 10) Minimal APIs, EF Core + Npgsql, JWT bearer auth,
  BCrypt.Net, Swagger.
- **Frontend:** React 19, TypeScript, Vite, TanStack Query, React Router, Axios.
  Console uses Ant Design; the storefront uses Tailwind CSS and Capacitor.
- **Database:** PostgreSQL 16.

## Payments

Payments live in a **separate microservice** — [`payment-service/`](payment-service/README.md) —
with its own solution, its own database and its own deployment. It exposes the API GentleStore
calls to take money, and hides the differences between payment gateways behind one port.

**[Paynet](https://paynet.am) is integrated** — ARCA cards, Idram and Telcell, registered as three
gateway keys (`paynet-arca`, `paynet-idram`, `paynet-telcell`) so a storefront can offer them side
by side. Around it sits the machinery a payment service needs regardless of provider: idempotency,
an attempt log, a webhook inbox, status-transition rules, a callback outbox and a reconciler.
Adding another provider means one class implementing `IPaymentGateway` and one line of
registration — nothing in GentleStore changes.

### Switching payments on for a tenant

Payments are **off for every store until a super admin turns them on**, one tenant at a time
(**Admin → Stores → ⋯ → Payments**).

Each store is pointed at **its own account** with the provider, so customers pay the merchant
directly and the platform never holds anyone else's money. The dialog asks for a *credential
reference* rather than an API key: the key itself lives in the payment service's secret store, and
neither this application, its logs nor its database ever sees it.

Activation writes the payment service first and flips the local flag last. A store whose flag said
"customers must pay" while no gateway was enrolled would block every checkout it has, so a failed
enrolment leaves payments off rather than half-on.

A method that cannot take the store's currency is shown but not selectable — Paynet is AMD-only, and
a USD store finding that out at a customer's checkout is far too late.

### What changes for a customer

| Store | Order lands as | Then |
|---|---|---|
| Payments off | `New` | Settled with the store directly, as before |
| Payments on | `AwaitingPayment` | Customer pays; the order becomes `New` once the money is captured |
| Payments on, "price on request" item | `AwaitingQuote` | Becomes `AwaitingPayment` when the store quotes it |

The storefront sends the customer to the provider's hosted page, so no card details touch this
application. On their return the order page **re-reads the payment from the gateway** rather than
trusting the query string they came back with — that was written by their own browser.

`PaymentRequired` is snapshotted onto the order at placement. Switching payments off tomorrow does
not turn an unpaid order into one nobody ever owed for, and switching them on does not retroactively
block orders already agreed by phone.

A store can only **cancel** an unpaid order, not wave it through to `Confirmed` — that would quietly
defeat the setting the platform admin switched on.

Run it alongside the rest of the stack:

```bash
docker compose up -d postgres payments     # http://localhost:5090/swagger
```

See the [service README](payment-service/README.md) for the API, the callback contract, the Paynet
specifics (AMD dram conversion, status mapping, webhook verification) and what writing another
gateway integration involves.

---

## Notes & future ideas

- Recording a payment taken out-of-band (cash on collection, bank transfer), so a store has an
  honest way to release an `AwaitingPayment` order instead of cancelling it.
- Refunds from the backoffice. The payment service supports them; nothing in the console calls them yet.
- Delivery fees, minimum orders and delivery zones — currently a free-text delivery note.
- Self-service cancellation for verified customers (deliberately not offered to unverified guests:
  a guessed order number should not let anyone cancel someone else's order).
- Per-customer language, so invite and order messages go out in the customer's language rather than
  the staff member's.
- Sending invite links over SMS as well as WhatsApp.
- Native session storage for the Capacitor build, where remote cookies are less reliable than in
  a browser.
- A retention policy for public-checkout contact details, which are PII collected from people with
  no prior relationship to the store.
- Order POSTs should also check the `Origin` header once money is involved; `SameSite=Lax` is the
  current CSRF floor.
- Cloud image storage (S3/Blob) instead of local files.
- Multi-currency conversion and i18n.
