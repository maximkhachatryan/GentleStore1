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
└─ frontend/                          # npm workspaces monorepo
   ├─ packages/shared/                # Typed API client + shared TS types
   └─ apps/
      ├─ console/                      # Admin + Backoffice (React + Ant Design)
      └─ storefront/                   # Public customer app (React + Tailwind + Capacitor)
```

**Multi-tenancy:** a single database with a `StoreId` discriminator on tenant-owned rows.
The backoffice is auto-scoped to the signed-in user's `StoreId`; the public API resolves the tenant
from the `{slug}` in the URL; the super-admin can act across all stores.

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
  by category / tag / search, view product details, and tap **WhatsApp** / **Call** to order.
- **Console → Super Admin**: dashboard stats, create/edit/activate stores, manage users.
- **Console → Store Owner**: edit store profile (with logo upload), manage categories, products
  (with image upload & tag assignment) and tags — all scoped to their own store.

---

## API overview

| Group        | Base path            | Auth                         |
|--------------|----------------------|------------------------------|
| Auth         | `/api/auth`          | anonymous (`login`) / bearer |
| Admin        | `/api/admin`         | `SuperAdmin`                 |
| Backoffice   | `/api/backoffice`    | `StoreOwner` / `StoreStaff`  |
| Public       | `/api/public`        | anonymous                    |
| Uploads      | `/api/uploads`       | any authenticated user       |

Uploaded images are stored under `backend/src/GentleStore.Api/wwwroot/uploads` and served at `/uploads/*`.

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

## Notes & future ideas

- Cart / checkout / orders, customer accounts and payments.
- Per-store subdomains or custom domains.
- Cloud image storage (S3/Blob) instead of local files.
- Multi-currency conversion and i18n.
