# Operations and release runbook

## Deployment

Use the Docker Compose setup for local infrastructure. For a real deployment, build/push the two Docker images, replace the image tags/hostnames in `infrastructure/kubernetes/app.yaml`, create the secrets and TLS certificate, then apply the manifest. These templates are not a provisioned cluster or a completed deployment.

Production API configuration:

- `ASPNETCORE_ENVIRONMENT=Production`
- `DatabaseProvider=SqlServer`
- `ConnectionStrings__Database` with a least-privilege application account, TLS encryption and a valid server certificate. The local Compose `sa` account and `TrustServerCertificate` are development conveniences.
- `TicketSigningKey`: a secret random value of at least 32 characters, consistent across API replicas. Losing/changing it invalidates existing QR signatures.
- `PublicOrigin=https://your-real-domain` for strict browser mutation origin checks.
- `Bootstrap__AdminEmail` and `Bootstrap__AdminPassword` to create the initial administrator, then remove the bootstrap password after first provisioning.
- Provider secrets for Stripe, Redis, RabbitMQ, and SMTP as needed.
- `Observability__MetricsToken` for authenticated internal Prometheus scrapes; configure `authorization.credentials_file` on the scraper.

Do not enable `SeedDemo` or use Development for public production. Rotate all sample credentials and use a secrets manager. Finish email verification, account recovery, MFA/OIDC integration, and bot-risk controls before live sales.

SQL Server runs migrations at startup. Keep one API replica during schema initialization; use a controlled migration job for subsequent production releases. Review the generated SQL before applying changes. SQLite is a local development option; schema changes require a fresh local database or a deliberately authored SQLite migration.

The Kubernetes template starts one API replica. For scale-out, configure a Redis SignalR backplane and session affinity, benchmark SQL locking, and validate worker/event duplicate handling. The durable outbox can be published more than once across workers; consumers must deduplicate IDs. Do not scale by copying SQLite files.

The API's per-user rate limit is 240 requests/minute; authentication is limited per direct connection IP. Nginx adds a separate network limit. Configure trusted reverse-proxy forwarding and distributed identity/IP limits for your actual gateway before large deployments. Do not blindly trust arbitrary `X-Forwarded-For` input.

## Payment uncertainty

1. Find the existing order in Admin → Payments.
2. Reconcile the stored attempt. Never create a new independent charge for a timeout.
3. Inspect the provider dashboard using the stored payment reference if the outcome remains unknown.
4. `ALLOCATED`/unknown-capture seats stay owned; do not manually release them until payment capture/void/refund is definitive.
5. A `CAPTURED_UNALLOCATED` exception needs a manual provider refund and an incident record containing the order/reference, amount, evidence, and settlement result. Automated fulfillment is intentionally blocked.

Refund approvals can remain `PROCESSING` if a provider reply is lost or settlement is pending. Use the same refund approval again to reconcile it. Do not reject a refund that has already been submitted to the provider.

## Outbox and notifications

- Track `eventhub_outbox_pending`, worker warnings, and the oldest unpublished row.
- If RabbitMQ is unavailable, outbox rows remain pending. Restore the broker; workers retry. Check downstream deduplication before replaying historical events.
- In-app notifications are persisted with the business result. Configure SMTP for email. Email delivery is at-least-once; repeated emails do not create repeated bookings.
- Redis/catalog failure must not block authoritative seat ownership. SignalR disconnects should trigger a fresh snapshot when clients reconnect.

## Backups and recovery

Back up SQL Server using the organization's tested backup strategy, including offsite retention. Back up the ticket signing key separately and restrict access. For local SQLite, stop the API and preserve the complete `data/` directory, including the signing key; do not copy a live WAL database with ordinary file copy and assume consistency.

Restore into an isolated environment, verify active-ticket uniqueness, reconcile all ambiguous payment attempts against the provider, validate refund settlements, and inspect outbox backlog before reopening sales. Define and drill RTO/RPO for the actual SQL Server topology. This code does not provision SQL high availability or certify a zero-loss recovery objective.

## Release acceptance

Require successful SQL Server integration and fault-injection tests, Stripe test-mode end-to-end validation, external refund/replay tests, browser transport/reconnect/accessibility testing, and representative load tests. Validate container health checks, certificate handling, proxy trust, monitoring auth, backup restoration, and roles. The blueprint's scale/availability numbers remain unmeasured targets.
