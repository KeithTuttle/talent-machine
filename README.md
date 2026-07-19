# The Talent Machine Company — Production Planner

Web app for directors and choreographers planning musical productions: seasons,
shows, numbers, casts, groups, and roles, with history across many years.
Stage-formation editing is planned next.

**Stack:** .NET 8 Web API + EF Core + PostgreSQL (Npgsql) · Vue 3 + Vite + Pinia +
Tailwind · Clerk auth · multi-tenant from day one.

## Layout

```
server/TalentMachine.Api   .NET 8 API (port 5186)
client/                    Vue 3 SPA  (port 5201, proxies /api → 5186)
docker-compose.yml         local Postgres 16 (host port 5433, db talentmachine)
```

## Setup

Prereqs: .NET 8 SDK, Node 20+, and either Docker (local Postgres) or a Supabase project.

```bash
cd server && dotnet tool restore && dotnet build
cd client && npm install
```

### Database

**Option A — local Docker:** `docker compose up -d`. The committed placeholder
connection string already points at it; nothing else to configure.

**Option B — Supabase:** create a project at supabase.com → **Connect** → copy the
**Session pooler** URI (the direct connection is IPv6-only on many networks).
Convert it to keyword form and put it in the gitignored
`server/TalentMachine.Api/appsettings.Development.local.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<db-password>;SSL Mode=Require"
  }
}
```

This file loads *after* user-secrets on purpose — VS Code's task runner can spawn
`dotnet run` in an environment where `%APPDATA%` user-secrets silently fail to
load; a file next to the app always loads. Never commit real secrets.

### Migrations (never automatic)

The API does **not** migrate on startup. Apply schema changes explicitly:

```bash
cd server/TalentMachine.Api
dotnet ef database update           # apply migrations to the configured DB
dotnet ef migrations add <Name>     # after changing the model
```

Always run via the pinned local tool (`dotnet tool restore` first) so a global
EF 9/10 tool can't generate mismatched migrations.

### Auth (Clerk) — optional in dev

Without Clerk config the app runs **open and unauthenticated** (rows carry
TenantId 0; they become invisible once real sign-ins exist). To enable auth:

1. Create an application at dashboard.clerk.com (Email and/or Google is fine).
2. Copy the **Publishable key** into `client/.env` (see `client/.env.example`):
   `VITE_CLERK_PUBLISHABLE_KEY=pk_test_...`
3. Copy the **Frontend API URL** (e.g. `https://your-app.clerk.accounts.dev`)
   into `appsettings.Development.local.json`:

```json
{
  "Clerk": {
    "Authority": "https://your-app.clerk.accounts.dev",
    "AuthorizedParty": "http://localhost:5201"
  }
}
```

No Clerk secret key is needed anywhere — the API validates JWTs via the
authority's public JWKS. First sign-in auto-provisions your tenant (you become
Owner). Team → invite code lets others join your tenant.

### Access model

- **Owner** (the company manager): full access to everything; creates seasons
  and shows; grants/revokes show access; owns exports.
- **Members** (directors, choreographers, music directors, producers…): join by
  invite code and collaborate **at the show level** — they only see and edit the
  productions they've been granted (an invite can carry one show; Owners grant
  more from the Team page). The performer roster is tenant-wide (casting needs
  it); seasons are readable but only Owners change them.

## Run

VS Code: **Ctrl+Shift+B** runs the default task *Start: client + API*. Manually:

```bash
cd server/TalentMachine.Api && dotnet run     # http://localhost:5186 (Swagger at /swagger)
cd client && npm run dev                      # http://localhost:5201
```

The API boots even without a reachable database (health check stays green; data
endpoints fail until it's configured). Reads that fail render empty states;
failed writes surface a toast.

## Verifying auth + tenancy end to end

1. With Clerk configured, sign in — check the `Tenants`/`Memberships` tables for
   your auto-provisioned row.
2. Data created earlier with auth off (TenantId 0) is now invisible — expected.
3. Team page: create an invite, sign in as a second user in a private window,
   redeem the code, and confirm both accounts see the same seasons.

## Deploy

- **API — Render:** Docker service from `server/TalentMachine.Api/Dockerfile`.
  Env vars: `ConnectionStrings__Default`, `Clerk__Authority`,
  `Clerk__AuthorizedParty=https://<client-domain>`,
  `Cors__AllowedOrigins=https://<client-domain>`. Render sets `PORT` itself.
  Run `dotnet ef database update` against the production DB before/with each
  deploy that includes a migration.
- **Client — Vercel:** project root `client/` (SPA rewrite is in `vercel.json`).
  Env vars: `VITE_API_URL=https://<api>.onrender.com`,
  `VITE_CLERK_PUBLISHABLE_KEY` (a production `pk_live_...`).
