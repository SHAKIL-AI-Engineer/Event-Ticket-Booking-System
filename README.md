# EventHub — Event Ticket Booking System

A complete runnable web application based on the supplied enterprise ticketing blueprint: event discovery, assigned-seat booking, checkout, QR tickets, organizer management, and administrative operations.

**Next.js 16 · React 19 · TypeScript · ASP.NET Core .NET 10 · EF Core · SQL Server / SQLite · SignalR**

## Run locally

Install **Node.js 24**, **.NET SDK 10**, and **Python 3.10+**. No database server or payment credentials are required for the local sandbox.

```bash
npm --prefix web ci
npm run dev
```

Open [http://localhost:3000](http://localhost:3000). The launcher starts Next.js and the API at port 5050. Development creates a persistent SQLite database under `backend/EventHub.Api/data/`, eight upcoming events, and four demo accounts. The UI has one-click demo sign-in buttons.

| Role          | Email                 | Local demo password |
| ------------- | --------------------- | ------------------- |
| Attendee      | `maya@example.com`    | `EventHub!2026`     |
| Organizer     | `host@eventhub.test`  | `EventHub!2026`     |
| Administrator | `admin@eventhub.test` | `EventHub!2026`     |
| Venue staff   | `gate@eventhub.test`  | `EventHub!2026`     |

These accounts are seeded only in Development or with an explicit `SeedDemo=true`. Newly registered customers can choose **Become an organizer**. Only administrators can grant staff or administrator access.

## Included features

- Search and filter events by city, category, date, and price; grid/list views and saved events.
- Event details, performer information, venue directions, policies, and availability.
- Interactive seat map, section prices, zoom, keyboard navigation, a textual seat list, and live SignalR updates.
- Server-authoritative five-minute holds, atomic seat groups, eight-ticket limits, transparent fees, and persistent retry keys.
- Waiting room with durable FIFO admission and server-validated admission tokens.
- Sandbox payments with success, decline, and delayed-capture scenarios; an optional Stripe adapter with manual capture, Payment Elements, signature-verified webhooks, and reconciliation.
- Order status and history, signed QR tickets, calendar downloads, printable PDF invoices, one-time transfers, and refund requests.
- Organizer dashboard with database-backed revenue/occupancy charts, event creation/editing/publishing/cancellation, venue creation, section pricing, attendee lists, and CSV export.
- Administrator dashboard for payment recovery, refund approval/rejection, account roles, and audit history.
- Online venue check-in with atomic duplicate prevention; optional camera QR scanning where the browser supports `BarcodeDetector`.
- Profile/password management, persistent in-app notifications, and optional SMTP email delivery.
- Durable outbox/inbox, optional RabbitMQ event publishing, Redis catalog caching and SignalR backplane, structured API errors, correlation IDs, rate limiting, and Prometheus metrics.
- Responsive customer and management pages, loading/empty/error states, dialog focus handling, and reduced-motion support.

## Verify

```bash
dotnet build EventHub.slnx
npm --prefix web run build
python scripts/test_api.py
```

The integration runner starts an isolated API/database, performs real HTTP requests, and checks database state. It covers 46 checks including a 16-user contested-seat race, overlapping groups, idempotency, signed webhook replay, server expiry, recovery after an API restart during ambiguous capture, refunds, transfers, and concurrent check-in. It cleans up its fixture.

For the SQL Server provider, with Docker and sufficient disk space:

```bash
docker pull mcr.microsoft.com/mssql/server:2022-latest
python scripts/test_api.py --sqlserver
```

The SQL test mode creates and removes its own container with an ephemeral generated password. GitHub Actions runs both database paths. See [validation results](docs/validation.md) for what was actually verified in the authoring environment.

## SQL Server and infrastructure

```bash
cp .env.example .env
# Replace the password/key placeholders in .env with unique values.
docker compose up --build
```

Open [http://localhost:8080](http://localhost:8080). Compose starts SQL Server, Redis, RabbitMQ, the API, Next.js, and Nginx. This is a **local sandbox configuration**, with development seed accounts and sandbox payments by default. SQL Server uses the checked-in EF migrations; SQLite uses `EnsureCreated` for a fresh development database.

Optional monitoring:

```bash
docker compose --profile observability up --build
```

Prometheus is at [localhost:9090](http://localhost:9090) and Grafana at [localhost:3001](http://localhost:3001). Production metrics require an administrator session or `Observability__MetricsToken` as a bearer token. Configure Prometheus credentials for that mode.

## Real payments and email

For Stripe, configure these API environment variables with keys from the same Stripe account and mode:

```text
Payments__Provider=Stripe
Payments__StripeSecretKey=<secret key>
Payments__StripePublishableKey=<publishable key>
Payments__StripeWebhookSecret=<webhook signing secret>
```

Forward PaymentIntent events to `POST /api/v1/webhooks/payments`. The webhook verifies the raw payload signature and timestamp, deduplicates the event, then retrieves the authoritative provider state. Card details are handled by Stripe.js. Separate authorization/capture currently supports **card payments**, not every Stripe payment method. Start with Stripe test mode.

For email, set `Smtp__Host`, `Smtp__Port`, `Smtp__From`, `Smtp__User`, `Smtp__Password`, and `Smtp__EnableSsl`. In-app notifications work without SMTP. No paid services are contacted in the default sandbox.

## Documentation

- [API reference and examples](docs/api.md); machine-readable specification at `/openapi/v1.json`.
- [Implementation and architectural decisions](docs/implementation.md).
- [Operational and deployment runbook](docs/operations.md).
- [Validation evidence and remaining limits](docs/validation.md).
- [Original supplied blueprint, with exported diagram styles removed](docs/architecture-blueprint.md).
- [Kubernetes deployment template](infrastructure/kubernetes/app.yaml).

The blueprint's 100,000 concurrent visitors and 99.95% availability are targets, **not benchmark results**. This repository implements the core web product and transactional workflows. Live provider certification, SQL Server failure/load tests, browser accessibility testing, production identity integration, infrastructure hardening, and disaster-recovery drills remain release work. React Native, OpenSearch, SMS/push, and offline check-in are not included.
