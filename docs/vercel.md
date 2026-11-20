# Vercel deployment

The root `vercel.json` deploys the Next.js app and a .NET container through Vercel Services (Beta). Ordered routes send `/api/v1`, `/hubs`, `/health`, and `/openapi` to the API. A Vercel Daemon runs the durable expiry, payment-recovery, and outbox worker independently of web requests.

The API's Vercel Dockerfile uses a service-local build context, supports `PORT`, and runs without a writable application directory. Local SQLite is deliberately rejected on Vercel. Next.js disables its localhost API rewrites on Vercel because public service routing owns those paths.

## Database and credentials

The project uses Neon PostgreSQL with TLS certificate verification and channel binding. Tables and EF migration history live in the `eventhub` schema. SQL Server and SQLite remain supported for other deployments. PostgreSQL has separate migrations and provider-specific constraints.

Configure encrypted environment variables in Vercel, never in source:

- `DATABASE_URL`: the Neon connection URL.
- `DatabaseProvider=Postgres` and `ASPNETCORE_ENVIRONMENT=Production`.
- `TicketSigningKey`: a stable random key shared by the API and daemon.
- `Bootstrap__AdminEmail`, `Bootstrap__AdminPassword`: initial administrator credentials. The project owner can manage these through Vercel's environment settings.
- `SeedCatalog=true`: adds sample events under a noninteractive curator account without creating public demo administrators.
- `Workers__Enabled=false`: web requests do not run continuous loops; the daemon overrides this to true.
- `Payments__Provider=Sandbox`: no real charges. Configure matching Stripe keys separately before enabling live payments.

Production and preview currently share the connected database. Use a separate Neon branch/database for future previews that may change data or schema. Preserve the project's access protection unless deliberately changing its audience.

Without a Redis backplane the deployed seat map uses labeled 15-second snapshots. Holds are always validated transactionally by PostgreSQL. Configure Redis for SignalR delivery across instances; then the client uses live subscriptions with periodic snapshot convergence.

## Verification

Run the web build, .NET build, the SQLite HTTP suite, the PostgreSQL HTTP suite, the payment race runner, and date formatting checks:

```bash
dotnet build EventHub.slnx
npm --prefix web ci
npm --prefix web run build
python scripts/test_api.py
python scripts/test_api.py --postgres
dotnet run --project backend/EventHub.Tests --no-build
node web/scripts/test-date.mjs
```

`--postgres` starts an isolated Docker database. `--postgres-native` can use native PostgreSQL binaries through `EVENTHUB_POSTGRES_BIN`. `--neon` securely reads a connection URL from stdin and attempts to create/drop an isolated `eventhub_test_*` database; it never targets the application's existing database for test mutations.

The authoring workspace cannot reach Neon's TCP port. Validate the deployed API's `/health`, catalog, authentication, hold/release, sandbox checkout, refund, and recovery workflow from Vercel. A READY frontend build alone is insufficient evidence that the database/API are healthy.
