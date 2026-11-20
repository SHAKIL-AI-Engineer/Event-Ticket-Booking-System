# Implementation decisions

## Product and modules

The repository is a modular monolith, as recommended by the blueprint for an initial team. `Program.cs` contains authentication/customer API routing; `Management.cs` contains role-protected management APIs. `BookingService` owns holds and orders; `PaymentService` owns payment/recovery/refunds; `PlatformWorker` handles expiry, reconciliation, event publication, and optional email. `AppDb` is the authoritative data model. The Next.js application exposes 22 customer, organizer, and operations routes.

Every visible statistic is calculated from persisted data. Seeded events are sample catalog content. There are no fabricated sales, attendees, revenue, or availability counters.

## Seat exclusivity

`Store.Write` opens a serializable database transaction. Seats are loaded in a consistent order, validated together, and updated within that transaction. The hold, immutable seat-price snapshots, idempotency response, audit record, and outbox message commit together. Selected SQLite busy errors and SQL Server deadlocks/unique conflicts receive bounded retries.

SQL Server serializable locking protects the read/write range; SQLite serializes writers. Database constraints enforce legal seat state/ownership combinations and positive prices. A filtered unique active-ticket index prevents two active tickets for an event-seat. This is independent of Redis, RabbitMQ, and the browser.

Expired holds can be reclaimed on a booking attempt. Cleanup releases only rows whose current `HoldId` still matches the expired hold, so an old worker cannot release a replacement reservation. `ALLOCATED` seats are excluded from hold expiry.

## Idempotency and payment recovery

Holds, orders, payment initiation, ticket transfers, event creation, and venue creation persist a user/scope/key record plus a hash of request input. A repeated key with different input returns 409. Other mutations have natural stable-resource state checks. The frontend preserves stable keys for checkout retries.

Each order has one logical payment attempt. Authorization, allocation, capture, and fulfillment are separate recoverable stages. Allocation commits before capture. A crash or unknown capture retains allocation; reconciliation retrieves the existing provider payment using the same stable provider idempotency key. A confirmed capture issues tickets in a database transaction. Capture status is not finalized separately before fulfillment, avoiding an unrecoverable “captured but no tickets” checkpoint.

A `CAPTURED_UNALLOCATED` state is retained as a manual financial exception; the admin UI never fabricates a successful booking. Staff must refund through the provider dashboard and record the incident, as described in the runbook. This repository does not automate that exceptional compensation.

Refund requests freeze transfer/check-in through the order state. Provider-confirmed approval revokes the tickets and releases the seats in one transaction. A processing refund cannot be rejected while the provider may already be settling it. Approval retries use the same refund identity at the provider.

## Real time, outbox, and integrations

The API returns a seat snapshot, then SignalR publishes small seat deltas containing increasing per-seat versions. Reconnection refreshes the authoritative snapshot. Availability notifications never determine ownership.

The outbox is committed with state changes. The worker broadcasts SignalR updates and, when configured, publishes durable RabbitMQ messages with publisher confirms. Failed publications remain pending for retry. Consumers must deduplicate message IDs. RabbitMQ is an outbound integration stream; the monolith's internal recovery workers also inspect persisted states directly and do not depend on a broker consumer to confirm orders.

Redis is optional for a short-lived catalog cache and the SignalR backplane. A cache failure falls back to database discovery; it cannot authorize a hold. Full-text discovery currently uses the application catalog, suitable for an initial inventory. OpenSearch indexing is future scaling work.

Notifications are always durable in-app records. SMTP email is optional and at-least-once; a crash after sending but before recording delivery can duplicate an email.

## Identity and ticket security

Local authentication uses ASP.NET password hashing, a random opaque session cookie, and a hashed server-side session record. Cookies are HTTP-only and become secure outside Development. Role checks run on the server using the current database role. Fetch Metadata rejects cross-site browser mutations; `PublicOrigin` adds an explicit origin check for production.

Tickets use a signature over the random ticket ID and token version, validated against current ownership/revocation and event/order state. Transfers increment the token version. Check-in validates the signature and atomically changes a valid ticket to checked-in; a unique scan constraint provides a second enforcement boundary. Online entry checks are restricted to the event's two-hour pre-entry window through its end.

Local password auth is implemented; OIDC/OAuth, email verification, password recovery, MFA, and risk-based bot challenges require further identity/provider integration before a public production launch.

## Deliberate runtime choices

- SQLite allows a complete local flow without infrastructure. SQL Server has its own migrations and optional integration runner.
- All prices are integer BDT minor units; the server fixes a 5% fee when a hold is created.
- Venues use a rectangular assigned-seat layout: up to 26 rows × 40 seats. Custom arenas and general-admission capacity allocation need separate inventory models.
- QR camera support uses the browser's optional BarcodeDetector API; text token entry is always available.
- Invoices are authenticated printable HTML that browsers can save as PDF.
- Native mobile clients, SMS/push, offline entry, and general admission are outside this repository's implemented web scope.
