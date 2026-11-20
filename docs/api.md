# API reference

Base path: `/api/v1`. Next.js and Nginx proxy to the ASP.NET Core API on the same origin. JSON uses camelCase. Sessions use the HTTP-only `eventhub_session` cookie.

Read the generated OpenAPI document at `/openapi/v1.json` for exact request/response schemas. Public event reads need no login; booking, ticket, and account APIs require a session. Organizer/admin/staff permissions are checked by the API, not only the UI.

## Endpoint groups

| Method      | Path                                               | Access / behavior                                                          |
| ----------- | -------------------------------------------------- | -------------------------------------------------------------------------- |
| GET         | `/config`                                          | Public payment mode and client configuration                               |
| POST        | `/auth/register`, `/auth/login`, `/auth/logout`    | Account sessions                                                           |
| GET         | `/auth/me`                                         | Current profile or `user: null`                                            |
| PUT         | `/account`, `/account/password`                    | Own profile/password                                                       |
| POST        | `/account/organizer`                               | Customer opts into organizer role                                          |
| GET         | `/events`                                          | Catalog; `q`, `category`, `city`, `from`, `to`, `maxPrice`, `sort`, `page` |
| GET         | `/events/{id}`, `/events/{id}/seats`               | Event detail / current availability                                        |
| GET         | `/venues`                                          | Venue catalog                                                              |
| POST        | `/events/{id}/queue`                               | Join or refresh durable admission                                          |
| GET         | `/events/{id}/queue`                               | Read existing queue entry                                                  |
| POST        | `/holds`                                           | Reserve 1–8 seats atomically                                               |
| GET, DELETE | `/holds/{id}`                                      | Own hold status / release                                                  |
| POST, GET   | `/orders`                                          | Create from own hold / own order history                                   |
| GET         | `/orders/{id}`                                     | Own authoritative status, seats, tickets, payment                          |
| POST        | `/orders/{id}/reconcile`                           | Recheck existing payment attempt                                           |
| GET         | `/orders/{id}/invoice`                             | Owner-authorized printable receipt                                         |
| POST        | `/payments/{orderId}/authorize`                    | Initiate/resume one logical attempt                                        |
| POST        | `/webhooks/payments`                               | Stripe-signed, timestamp-checked payment events                            |
| POST        | `/orders/{id}/refund`                              | Own eligible confirmed order                                               |
| GET         | `/tickets/me`                                      | Own tickets with signed QR tokens                                          |
| GET         | `/tickets/{id}/qr`, `/tickets/{id}/calendar`       | Own QR SVG / calendar file                                                 |
| POST        | `/tickets/{id}/transfer`                           | One-time transfer to registered email                                      |
| POST        | `/tickets/check-in`                                | Staff/admin or owning organizer; validates signature and event             |
| GET         | `/favorites`                                       | Own saved event IDs                                                        |
| PUT, DELETE | `/favorites/{eventId}`                             | Save / remove event                                                        |
| GET         | `/notifications`                                   | Own persistent notifications                                               |
| PUT         | `/notifications/read`                              | Mark own notifications read                                                |
| GET         | `/organizer/overview`                              | Revenue, occupancy, daily chart, recent orders                             |
| GET, POST   | `/organizer/events`                                | Owned events / create event                                                |
| PUT         | `/organizer/events/{id}`                           | Edit owned event; protects occupied inventory                              |
| PUT         | `/organizer/events/{id}/prices`                    | Price available seats in a section                                         |
| POST        | `/organizer/events/{id}/cancel`                    | Cancel and create refund requests                                          |
| GET         | `/organizer/events/{id}/attendees`                 | Scoped guest list; global venue staff can also read                        |
| POST        | `/organizer/venues`                                | Create rectangular seating plan                                            |
| GET         | `/staff/events`                                    | Events available to the current check-in role                              |
| GET         | `/admin/overview`, `/admin/orders`, `/admin/users` | Operations, exceptions, audit, refunds, users                              |
| PUT         | `/admin/users/{id}/role`                           | Grant role; self-demotion is forbidden                                     |
| POST        | `/admin/payments/{orderId}/reconcile`              | Reconcile existing payment                                                 |
| POST        | `/admin/refunds/{id}/review`                       | `{ "approve": true/false }`                                                |

SignalR hub: `/hubs/availability`; invoke `Subscribe(eventId)`, receive `SeatsChanged` messages with `{eventId, seats}`. Seat versions let clients ignore duplicate/stale messages.

`GET /health` checks database connectivity; `GET /metrics` exposes Prometheus metrics (administrator or configured metrics bearer token outside Development).

## Booking example

```bash
curl -c cookies.txt -H 'Content-Type: application/json' \
  -d '{"email":"maya@example.com","password":"EventHub!2026"}' \
  http://localhost:5050/api/v1/auth/login

curl -b cookies.txt -H 'Content-Type: application/json' \
  -H 'Idempotency-Key: your-unique-hold-request' \
  -d '{"eventId":1,"seatIds":[73,74]}' \
  http://localhost:5050/api/v1/holds
```

Create an order with `{ "holdId": "<returned-id>" }`, then post `{ "outcome": "success" }` to `/payments/{orderId}/authorize`. Sandbox outcomes are `success`, `decline`, or `delayed`. Live Stripe mode ignores the sandbox choice and returns a client secret when Payment Elements confirmation is required. Never store card data in your own API.

## Retry and error contract

Send `Idempotency-Key` (max 128 characters) on holds, orders, payment initiation, transfers, event creation, and venue creation. Preserve the same key and body after a transport failure. A reused key with different input returns `409 IDEMPOTENCY_MISMATCH`.

Errors contain `type`, `title`, `status`, `code`, `correlationId`, `retryable`, and optional `details`. Typical responses: `400` validation, `401` login required, `403` role/admission/CSRF rejected, `404` inaccessible resource, `409` conflicting seat or state, `410` expired hold, `429` rate limit, and `503` temporary service failure. Payment declines are persisted and returned as an order with `status: FAILED` plus `payment.error`; callers must inspect authoritative order state.

Timestamps represent UTC. UI dates are formatted in Asia/Dhaka. Monetary values are integer BDT minor units: `150000` means ৳1,500.00. Never calculate the payable total from client-provided prices.
