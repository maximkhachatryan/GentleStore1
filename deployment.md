# Deploying GentleStore to Hetzner (with a domain + HTTPS)

This guide walks you through deploying the full GentleStore stack (PostgreSQL, .NET API, the
console and storefront frontends, and an nginx reverse proxy with automatic HTTPS) to a single
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
| `nginx-proxy`    | `nginxproxy/nginx-proxy` | **Yes (80/443)**    | Reverse proxy; routes by domain                     |
| `acme-companion` | `nginxproxy/acme-companion` | No               | Issues/renews Let's Encrypt TLS certificates        |

`nginx-proxy` reads each service's `VIRTUAL_HOST` / `LETSENCRYPT_HOST` env vars and automatically
generates the vhosts and certificates — you don't hand-write any nginx config.

```
                       ┌────────────── Hetzner VM ──────────────┐
Internet ── 80/443 ──▶ │  nginx-proxy ──┬─▶ storefront (:80)     │
                       │                ├─▶ console    (:80)     │
                       │                └─▶ api        (:8080) ──┼─▶ postgres (:5432)
                       │  acme-companion (TLS certs)             │
                       └─────────────────────────────────────────┘
```

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

---

## 10. Build and start the stack

```bash
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

The first run will:

1. Build the API and both frontend images (this takes a few minutes).
2. Start Postgres and wait until it's healthy.
3. Start the API, which **applies EF Core migrations** and **creates the super admin** on boot.
4. Start `nginx-proxy`, then `acme-companion` requests TLS certificates from Let's Encrypt.

Check status and logs:

```bash
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs -f api
docker compose -f docker-compose.prod.yml logs -f acme-companion
```

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

## 14. Troubleshooting

**HTTPS / certificate not issued**
- Confirm DNS resolves to the server (`nslookup <domain>`), and ports **80 and 443** are open in the
  Hetzner firewall — Let's Encrypt validates over HTTP on port 80.
- Watch the ACME logs: `dcp logs -f acme-companion`.
- Ensure `ACME_EMAIL` is a real address and each domain's `LETSENCRYPT_HOST` matches its DNS name.

**502 Bad Gateway from nginx-proxy**
- The upstream container may still be starting or unhealthy: `dcp ps`, `dcp logs api`.
- Confirm the service exposes the right port (`VIRTUAL_PORT` is `8080` for the API, `80` for the SPAs).

**413 Content Too Large when uploading an image**
- nginx caps request bodies at **1 MB** by default and the `nginx-proxy` image does not override it,
  so the upload is rejected before it reaches the API. `nginx-proxy.conf` (mounted into
  `/etc/nginx/conf.d/`) raises this to 10 MB — make sure that file exists on the server and recreate
  the proxy: `dcp up -d nginx-proxy`.
- Verify it took effect:
  `docker exec gentlestore-nginx-proxy grep -r client_max_body_size /etc/nginx/conf.d`.
- Above 10 MB the request is still refused by nginx; between 5 and 10 MB the API answers with a
  readable JSON error. The API's own cap lives in `UploadsController.MaxBytes`.

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
