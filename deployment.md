# Deploying GentleStore to Hetzner (with a domain + HTTPS)

This guide walks you through deploying the full GentleStore stack (PostgreSQL, .NET API, the
console and storefront frontends, and a Caddy reverse proxy with automatic HTTPS) to a single
**Hetzner Cloud** VM using Docker Compose.

> No domain yet? See [DEPLOYMENT-without-domain.md](DEPLOYMENT-without-domain.md) to deploy by raw IP
> over plain HTTP (testing/staging only).

---

## 1. What gets deployed

The production stack is defined in [`docker-compose.prod.yml`](docker-compose.prod.yml):

| Service          | Image / build            | Exposed to internet | Purpose                                            |
|------------------|--------------------------|---------------------|----------------------------------------------------|
| `postgres`       | `postgres:16`            | No (internal only)  | Database + persistent `gentlestore_pgdata` volume  |
| `api`            | built from `backend/`    | No (via proxy)      | ASP.NET Core API on port 8080                       |
| `console`        | built from `frontend/`   | No (via proxy)      | Admin + backoffice SPA (nginx on port 80)           |
| `storefront`     | built from `frontend/`   | No (via proxy)      | Public storefront SPA (nginx on port 80)            |
| `payments`       | built from `payment-service/` | No (unless published) | Payment microservice                       |
| `caddy`          | `caddy:2.10-alpine`      | **Yes (80/443)**    | Reverse proxy; routes by domain, issues/renews Let's Encrypt certificates |

Caddy's routing lives in the [`Caddyfile`](Caddyfile) and reads the domains from `.env`.
It gets certificates for the platform's three hosts when it starts. It gets a certificate for a
**store's own domain** the first time someone visits it, after asking the API whether a store owns
that name (see §13a).

```
                       ┌────────────── Hetzner VM ──────────────┐
Internet ── 80/443 ──▶ │  caddy ──┬─▶ storefront (:80)           │
                       │          ├─▶ console    (:80)           │
                       │          └─▶ api        (:8080) ────────┼─▶ postgres (:5432)
                       └─────────────────────────────────────────┘
```

The storefront's nginx also forwards a few requests to the API on its own: each page's title,
description and search-engine tags (spliced into the HTML), `robots.txt`, `sitemap.xml` and
link-preview images.

---

## 2. Prerequisites

Before you start, make sure you have:

- A **Hetzner Cloud** account: <https://console.hetzner.cloud/>
- A **registered domain** you control (for DNS records).
- Three hostnames you'll point at the server, e.g.:
  - `example.com` → storefront
  - `console.example.com` → console
  - `api.example.com` → API
- An **SSH key pair** on your local (Windows) machine. If you don't have one:

  ```powershell
  # Run in PowerShell on your local machine
  ssh-keygen -t ed25519 -C "you@example.com"
  # Public key is printed by:
  Get-Content $env:USERPROFILE\.ssh\id_ed25519.pub
  ```

---

## 3. Create the Hetzner Cloud server

1. In the Hetzner Cloud Console, create a **Project** (e.g. `gentlestore`).
2. **Add Server**:
   - **Location:** closest to your users (e.g. Nuremberg / Falkenstein / Helsinki).
   - **Image:** **Ubuntu 24.04**.
   - **Type:** shared vCPU **CX22** (2 vCPU / 4 GB) is enough to start; use **CX32** (4 vCPU / 8 GB)
     if you expect more traffic or want faster image builds.
   - **SSH key:** paste the public key from the previous step. (Avoid password login.)
   - **Name:** e.g. `gentlestore-prod`.
3. Create the server and note its **public IPv4 address**.

---

## 4. Point DNS at the server

At your DNS provider, create **A records** pointing to the server's IPv4:

| Type | Name (host)     | Value (IP)        |
|------|-----------------|-------------------|
| A    | `@`             | `<server-ipv4>`   |
| A    | `console`       | `<server-ipv4>`   |
| A    | `api`           | `<server-ipv4>`   |

> DNS **must** resolve to the server **before** you start the stack, otherwise Let's Encrypt cannot
> validate the domains and certificate issuance will fail. Verify with:
>
> ```powershell
> nslookup example.com
> nslookup console.example.com
> nslookup api.example.com
> ```

---

## 5. Configure the Hetzner Cloud Firewall

In the Cloud Console → **Firewalls**, create a firewall and apply it to the server. Allow only:

| Direction | Protocol | Port | Source      | Purpose      |
|-----------|----------|------|-------------|--------------|
| Inbound   | TCP      | 22   | your IP¹    | SSH          |
| Inbound   | TCP      | 80   | Any (0.0.0.0/0, ::/0) | HTTP → HTTPS redirect + ACME |
| Inbound   | TCP      | 443  | Any         | HTTPS        |
| Inbound   | UDP      | 443  | Any         | HTTP/3 (optional) |

¹ Restrict SSH to your own IP if it's static; otherwise leave `Any` but keep key-only auth.

Postgres (5432) is **not** exposed — it's only reachable inside the Docker network.

---

## 6. First-time server setup

SSH in as root using your key:

```powershell
ssh root@<server-ipv4>
```

Then, on the server, run the following (bash):

```bash
# Update the OS
apt update && apt upgrade -y

# Create a non-root sudo user and copy your SSH key to it
adduser --disabled-password --gecos "" deploy
usermod -aG sudo deploy
rsync --archive --chown=deploy:deploy ~/.ssh /home/deploy

# Optional but recommended: enable automatic security updates
apt install -y unattended-upgrades
dpkg-reconfigure -f noninteractive unattended-upgrades
```

Log out and back in as the new user for the remaining steps:

```powershell
ssh deploy@<server-ipv4>
```

---

## 7. Install Docker + Compose plugin

On the server (as `deploy`):

```bash
# Install Docker Engine + Compose plugin from Docker's official repo
curl -fsSL https://get.docker.com | sudo sh

# Run docker without sudo (re-login required afterwards)
sudo usermod -aG docker $USER

# Apply the group change without a full logout
newgrp docker

# Verify
docker --version
docker compose version
```

---

## 8. Get the code onto the server

Clone your repository (or use a deploy key / private repo token as appropriate):

```bash
cd ~
git clone <your-repo-url> gentlestore
cd gentlestore
```

> If the repo is private, set up a read-only **deploy key** on the server and add it to your Git
> host, or clone over HTTPS with a personal access token.

---

## 9. Create the production `.env`

Copy the template and fill in **real** values. This file is git-ignored and must never be committed.

```bash
cp .env.example .env

# Generate strong secrets
openssl rand -base64 48   # use for JWT_SECRET
openssl rand -base64 24   # use for POSTGRES_PASSWORD

nano .env
```

Fill it in like so (replace every `change-me` and the example domains):

```dotenv
# --- Database ---
POSTGRES_DB=gentlestore
POSTGRES_USER=gentlestore
POSTGRES_PASSWORD=<paste a strong random password>

# --- JWT (>= 32 chars) ---
JWT_SECRET=<paste `openssl rand -base64 48` output>

# --- Public domains (must match your DNS A-records) ---
STOREFRONT_DOMAIN=example.com
CONSOLE_DOMAIN=console.example.com
API_DOMAIN=api.example.com

# --- Let's Encrypt notification email ---
ACME_EMAIL=you@example.com

# --- Initial super admin (created on first boot) ---
SUPERADMIN_EMAIL=you@example.com
SUPERADMIN_PASSWORD=<strong admin password>

# --- Keep demo data OFF in production ---
SEED_DEMO_DATA=false

# --- Invite links for invite-only storefronts (optional, defaults to 14) ---
INVITE_EXPIRY_DAYS=14
```

> **Important:** set `SUPERADMIN_PASSWORD` to a strong value **before** the first boot. The admin is
> created only once (when no super admin exists); changing this value later will **not** update an
> existing admin's password.

The frontends bake `VITE_API_URL=https://<API_DOMAIN>` at **build time**, so if you ever change
`API_DOMAIN` you must rebuild the frontends (see §12).

### Keep the API on a subdomain of the storefront

Invite-only storefronts sign customers in with a cookie the API sets and the storefront reads.
Browsers only send that cookie along if the two hosts are **same-site**, i.e. share a registrable
domain — which the layout above satisfies (`api.example.com` and `example.com`).

If you ever put the API on an unrelated domain (say `example.com` for the storefront and
`gentlestore-api.net` for the API), the cookie will be dropped and every invite-only storefront
will look permanently locked. In that case the cookie has to be relaxed in
`docker-compose.prod.yml`:

```yaml
      Storefront__SessionCookie__SameSite: "None"   # requires Secure, i.e. HTTPS
```

Same-site hosts are the better setup — `SameSite=None` gives up the CSRF protection that `Lax`
provides for free.

Stores on their **own domain** don't depend on this: there the storefront calls the API through the
store's domain (Caddy forwards `/api/public/*` and `/uploads/*`), so the cookie is first-party.
Leave `Storefront__SessionCookie__Domain` unset, so each cookie belongs to the host that set it.

---

## 10. Build and start the stack

```bash
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

The first run will:

1. Build the API and both frontend images (this takes a few minutes).
2. Start Postgres and wait until it's healthy.
3. Start the API, which **applies EF Core migrations** and **creates the super admin** on boot.
4. Start Caddy, which requests TLS certificates from Let's Encrypt for the three domains.

Check status and logs:

```bash
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs -f api
docker compose -f docker-compose.prod.yml logs -f caddy
```

### Upgrading a server that ran the old nginx-proxy stack

Earlier versions used `nginx-proxy` + `acme-companion` instead of Caddy. Those containers still hold
ports 80 and 443 until they are removed, so the first deploy after the switch must remove them too:

```bash
git pull
docker compose -f docker-compose.prod.yml --env-file .env up -d --build --remove-orphans
```

Caddy requests fresh certificates for every domain on its first start, which takes a minute or two.
The old `nginx_*` and `acme_state` volumes are no longer used; once the site works over HTTPS,
`docker volume ls` shows them and `docker volume rm` deletes them.

---

## 11. Verify the deployment

Certificate issuance can take 1–2 minutes after the containers are up. Then check:

- `https://example.com` → storefront loads over HTTPS.
- `https://console.example.com` → console login page.
- `https://api.example.com/health` → API health endpoint responds.

From the server you can also curl:

```bash
curl -I https://api.example.com/health
```

If HTTPS doesn't work yet, see **Troubleshooting** below.

---

## 12. First login

1. Open `https://console.example.com`.
2. Log in with the `SUPERADMIN_EMAIL` / `SUPERADMIN_PASSWORD` from your `.env`.
3. Create your stores, users, and catalog from the admin panel.

---

## 13. Day-2 operations

All commands run from `~/gentlestore` on the server. Define a short alias to save typing:

```bash
alias dcp='docker compose -f docker-compose.prod.yml --env-file .env'
```

### Deploy new code

```bash
git pull
dcp up -d --build          # rebuilds changed images and recreates containers
```

> Use `up -d --build`, **not** `start` — `start` only restarts the existing (stale) containers and
> will not pick up code changes. Migrations run automatically on API startup.

### View logs / restart / stop

```bash
dcp logs -f api            # follow API logs
dcp restart api            # restart a single service
dcp down                   # stop and remove containers (volumes are kept)
```

### Rebuild frontends after changing the API domain

The API URL is compiled into the SPA bundle, so a domain change requires a rebuild:

```bash
dcp build --no-cache console storefront
dcp up -d console storefront
```

### Back up the database

```bash
# Dump to a timestamped file on the host
docker exec gentlestore-postgres \
  pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB" > backup-$(date +%F).sql
```

> `POSTGRES_USER` / `POSTGRES_DB` come from your `.env`; run `source .env` first if the variables
> aren't in your shell, or substitute the literal values.

Restore into a running database:

```bash
cat backup-2026-08-05.sql | docker exec -i gentlestore-postgres \
  psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"
```

### Back up uploaded images

Uploaded files live in the `gentlestore_uploads` Docker volume:

```bash
docker run --rm \
  -v gentlestore_uploads:/data \
  -v "$PWD":/backup alpine \
  tar czf /backup/uploads-$(date +%F).tar.gz -C /data .
```

> Consider scheduling both backups with `cron` and copying them off the server (e.g. to Hetzner
> Storage Box / object storage).

---

## 13a. Giving a store its own domain

A store can have its storefront on its own domain (`flowers.am`) instead of at
`example.com/flowers`. Nothing on the server changes: no `.env` edit, no restart.

1. **The store owner points the domain at the server.** At their DNS provider:

   | Type  | Name  | Value           |
   |-------|-------|-----------------|
   | A     | `@`   | `<server-ipv4>` |
   | CNAME | `www` | `flowers.am.`   |

   Do this first, and wait until `nslookup flowers.am` answers with the server's address.
2. **Enter the domain in the console:** Admin → Stores → ⋯ → Edit → **Own domain**. It is stored
   without `www.`; `www.flowers.am` redirects to `flowers.am`.
3. **Open `https://flowers.am`.** The very first request takes a few seconds while Caddy obtains the
   certificate; every request after that is normal.

Caddy only obtains certificates for domains a store owns: before requesting one it asks the API
(`/api/internal/tls/allowed`, reachable only from inside the compose network). Pointing some other
domain at the server gets a TLS error, not a certificate.

What changes once the domain is saved:

- The store's pages sit at the root of its domain: `flowers.am/product/<id>`, `flowers.am/cart`.
- For a **public** store, its catalogue pages on the platform (`example.com/flowers/…`) send visitors
  to the same page on the domain, and tell search engines that the domain's copy is the one to index.
  That is why DNS comes first: saved before the domain resolves, the store would be sending its
  visitors to an address that does not answer yet.
- Invite links, payment return links and the console's share links use the domain from then on.
  Order pages and invite links sent earlier keep working at the old address.
- **Sessions do not move with the store.** A browser signed in at `example.com/flowers` (an invited
  customer, a returning guest) is a stranger to `flowers.am`, because cookies belong to the domain
  that set them. That is why invite-only stores are not redirected: their customers keep using the
  old address until they get a new invite link, which points at the domain.

For search engines, the store owner can verify the domain in
[Google Search Console](https://search.google.com/search-console) (a DNS TXT record) and submit
`https://flowers.am/sitemap.xml`.

**If the domain shows a TLS error**

- Check that DNS answers with the server's address: `nslookup flowers.am`.
- Check that the API vouches for the name. This must print `HTTP/1.1 200 OK`; a 404 means no
  *active* store has that domain saved:
  ```bash
  docker exec gentlestore-caddy wget -S -O /dev/null "http://api:8080/api/internal/tls/allowed?domain=flowers.am"
  ```
- Look for the name in Caddy's log: `dcp logs caddy | grep flowers.am`. After repeated failures
  Let's Encrypt makes Caddy wait before it tries the same name again.

---

## 14. Troubleshooting

**HTTPS / certificate not issued**
- Confirm DNS resolves to the server (`nslookup <domain>`), and ports **80 and 443** are open in the
  Hetzner firewall — Let's Encrypt validates over port 80 or 443.
- Watch Caddy's logs: `dcp logs -f caddy`.
- Ensure `ACME_EMAIL` is a real address and the three domains in `.env` match their DNS names.
- For a store's own domain, see §13a.

**502 Bad Gateway from Caddy**
- The upstream container may still be starting or unhealthy: `dcp ps`, `dcp logs api`.
- The Caddyfile reaches services by name and port (`api:8080`, `storefront:80`, `console:80`), so
  a renamed service needs the Caddyfile changed too.

**"Ports are already allocated" when Caddy starts**
- The old `nginx-proxy` container is still running. See "Upgrading a server that ran the old
  nginx-proxy stack" in §10.

**Uploads over 30 MB are refused**
- That limit is the API's own (`UploadsController.MaxBytes`), and it answers with a readable JSON
  error. Caddy sets no body-size limit of its own.

**API can't reach the database**
- Check `dcp logs postgres` for readiness and that the `ConnectionStrings__Default` host is
  `postgres` (the service name), not `localhost`.

**An invite link opens but the storefront stays locked**
- The session cookie is not surviving the round trip. Open the link and check the browser's
  network tab: the `POST /access/redeem` response must carry a `Set-Cookie: gs_sf_…` header, and
  the following `GET /api/public/stores/<slug>` must send it back.
- If the cookie is set but never sent back, the API and storefront are not same-site — see
  "Keep the API on a subdomain of the storefront" in §9.
- If the response has no `Set-Cookie` at all, `Cors__Origins` is missing the storefront origin, so
  the browser discarded a credentialed response.
- Private/incognito windows discard the cookie when closed, so a link opened there needs
  regenerating. This is expected, and the storefront says so on the locked screen.

**A customer says their link says "already in use"**
- The link was opened somewhere else first — a link preview bot in a group chat, or a forwarded
  copy. Generate a new one from **Customers → New link**; the old one is revoked automatically.
- Invite history for that customer (**Customers → Customer details**) shows when each link was
  opened and by which browser.

**Admin login fails**
- The admin is created only on the first boot with an empty user table. If you booted once with a
  placeholder password, reset it via SQL:
  ```bash
  docker exec -it gentlestore-postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"
  # then update the Users row for your SuperAdmin, or delete it and restart the API to recreate it.
  ```

**Changed `.env` but nothing changed**
- Recreate the containers so new env values apply: `dcp up -d`. Frontend/API-domain changes also
  need a rebuild (see §12).

---

## 15. Optional hardening

- **UFW** on the host as a second layer (allow 22/80/443, deny the rest).
- **fail2ban** to throttle SSH brute-force attempts.
- A small **swap file** on 4 GB machines to avoid OOM during image builds:
  ```bash
  sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile
  sudo mkswap /swapfile && sudo swapon /swapfile
  echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
  ```
- Take periodic **Hetzner snapshots/backups** of the server volume.
