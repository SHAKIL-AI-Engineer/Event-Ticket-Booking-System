# Validation evidence

## Completed in the authoring environment

- ASP.NET Core .NET 10 build succeeded with zero warnings/errors, including resolved dependency audit warnings.
- Next.js production build succeeded and generated all defined routes.
- TypeScript type checking passed.
- 46 integration checks passed against isolated SQLite, using real HTTP requests and direct database assertions.
- The suite includes 16 separate authenticated users competing for a single seat; exactly one hold succeeded.
- Tests verify atomic overlapping groups, request fingerprint validation, repeat order/payment initiation, unique active tickets, signed webhook deduplication, expired hold rejection, stale cleanup ownership, declined payment release, ambiguous capture retention/recovery, approved refunds, queue enforcement, transfer signature invalidation, simultaneous duplicate check-in, and persisted analytics/notifications/audits.

The runner also contains an API restart during an unresolved sandbox capture to exercise durable recovery. Check current CI results for the final revision's results.

## Environment limits

- The managed browser preview could not open its relay port. Browser interaction, screenshots, responsive visual inspection, and screen-reader verification were not completed here.
- The SQL Server image exhausted the workspace's 8 GB filesystem before a container could start. The downloaded image was removed. SQL Server migrations and the SQL integration runner are included, but no successful SQL Server execution or benchmark is claimed from this environment.
- Stripe live/test credentials and SMTP credentials were not supplied. Their adapters compile; external provider end-to-end transactions were not executed.
- Redis/RabbitMQ/SignalR backplane, container images, Kubernetes, and monitoring manifests require deployment verification. Local in-process SignalR is implemented, but browser reconnect/transport behavior was not interactively verified here.

## Release acceptance still required

Run SQL Server concurrency and rollback tests, provider failure/timeout/replay tests, browser/mobile/accessibility tests, and backup/restore drills in the actual deployment. Obtain measured latency/capacity results before describing the system as ready for 100,000 concurrent visitors or a 99.95% availability target. Complete identity hardening and financial exception procedures before live sales.
