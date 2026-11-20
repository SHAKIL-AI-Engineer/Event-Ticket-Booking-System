# Enterprise Event Ticket Booking System — Advanced System Design & Architecture

Production-grade architecture blueprint | HLD + LLD + ERD + UI/UX + Distributed Transactions + High Performance

We will design a high-concurrency event booking platform similar in functionality to Ticketmaster or BookMyShow, capable of handling intense ticket-sale traffic without selling the same seat twice.

The architecture will cover customers, event organizers, ticketing administrators, payment processing, live seat availability, temporary holds, ticket issuance, and venue entry validation.

বাংলায়: আমরা এমন একটি Enterprise Ticket Booking System ডিজাইন করব যেখানে হাজার হাজার মানুষ একই সময়ে টিকিট বুক করার চেষ্টা করলেও একই সিট দুইজনের কাছে বিক্রি হবে না। পেমেন্ট সমস্যা, সার্ভার ডাউন, নেটওয়ার্ক ল্যাগ এবং রিয়েল-টাইম সিট আপডেট—সবকিছুর সমাধান থাকবে।

## 1. Project overview and technology decisions

ILLUSTRATIVE PEAK SCENARIO

## 100,000 concurrent visitors

10,000 seats for one popular concert

## 5 min

Seat hold duration

## 0

Permitted double-sales

## 99.95%

Availability target

These are proposed engineering requirements, not measured production results. Capacity and availability require load testing and infrastructure validation.

### Recommended technology stack

| Layer          | Technology                           | Responsibility                          |
| -------------- | ------------------------------------ | --------------------------------------- |
| Web frontend   | Next.js + TypeScript                 | Event discovery and booking             |
| UI framework   | Tailwind CSS + shadcn/ui             | Responsive, accessible components       |
| Animation      | Framer Motion / Motion               | Transitions and microinteractions       |
| Mobile         | React Native                         | Customer tickets and booking            |
| Backend        | ASP.NET Core .NET 10                 | Booking and business APIs               |
| Gateway        | YARP / Nginx                         | Routing and protection                  |
| Database       | SQL Server                           | Authoritative inventory and orders      |
| Cache          | Redis                                | Event caching, availability projections |
| Message broker | RabbitMQ                             | Reliable asynchronous workflows         |
| Live updates   | ASP.NET Core SignalR                 | Real-time seat and order events         |
| Payments       | Provider adapter                     | Authorization, capture, refunds         |
| Search         | OpenSearch                           | Event discovery                         |
| Observability  | OpenTelemetry + Prometheus + Grafana | Performance and incident visibility     |
| Infrastructure | Docker + Kubernetes                  | Deployment and scalability              |

Architectural decision: Start with a modular monolith for a small team, with isolated Booking/Inventory and Payment modules. Extract services as operational needs grow. Microservices are not automatically faster or safer.

## 2. High-Level Design (HLD)

System-wide component architecture


### Important architectural separation

The most critical part is the Inventory & Hold Service.

It must provide a strongly consistent answer to the question:

> Who currently has the exclusive right to purchase seat A-12 for event 501?

That answer must come from the authoritative transactional database, not from a browser, WebSocket message, or eventually consistent Redis cache.

Microsoft's SQL Server documentation describes how transactional writes and row-versioned reads interact, including conflict detection under different isolation levels. This is relevant when implementing a contested-seat booking path.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://learn.microsoft.com\&sz=32)

Microsoft Learn+1



### Core architecture rules

1. One authoritative inventory state per event-seat.
2. Booking an entire group of seats must succeed or fail atomically.
3. An expired hold must never be accepted solely because the UI still shows it.
4. Payment and seat allocation require a recoverable workflow, not a distributed database transaction across the payment provider.
5. Notifications, analytics, and real-time broadcasts must not delay the critical booking transaction.
6. All operations that can be retried need idempotency and clearly defined state transitions.

These are the primary guarantees on which the rest of the design is built.

## 3. Low-Level Design (LLD)

The HLD describes which systems exist. The LLD describes how classes, transactions, methods, and state transitions cooperate inside those systems.


### Backend project organization

```
TicketPlatform/
├── src/
│   ├── ApiGateway/
│   ├── BuildingBlocks/
│   │   ├── Contracts/
│   │   ├── Security/
│   │   ├── Observability/
│   │   └── Messaging/
│   ├── Modules/
│   │   ├── Identity/
│   │   ├── EventCatalog/
│   │   ├── VenueManagement/
│   │   ├── SeatInventory/
│   │   ├── Booking/
│   │   ├── Payment/
│   │   ├── Ticketing/
│   │   ├── Notifications/
│   │   ├── CheckIn/
│   │   └── Analytics/
│   └── Workers/
│       ├── HoldExpiryWorker/
│       ├── PaymentRecoveryWorker/
│       ├── OutboxPublisher/
│       └── NotificationWorker/
├── web/
│   ├── app/
│   ├── components/
│   │   ├── events/
│   │   ├── seat-map/
│   │   ├── checkout/
│   │   └── tickets/
│   └── lib/
├── tests/
│   ├── Unit/
│   ├── Integration/
│   ├── Concurrency/
│   ├── Contract/
│   └── Performance/
└── infrastructure/
    ├── docker/
    ├── kubernetes/
    └── monitoring/
```

### Important application components

| Component               | Method                      | Function                             |
| ----------------------- | --------------------------- | ------------------------------------ |
| HoldService             | `CreateHoldAsync()`         | Reserve requested seats              |
| HoldService             | `ReleaseHoldAsync()`        | Release an active hold               |
| SeatInventoryRepository | `TryAcquireSeatsAsync()`    | Atomically claim seat inventory      |
| PricingService          | `CalculateQuoteAsync()`     | Apply price rules and taxes          |
| PaymentOrchestrator     | `AuthorizeAsync()`          | Request payment authorization        |
| PaymentOrchestrator     | `CaptureAsync()`            | Capture authorized funds             |
| OrderService            | `ConfirmAllocationAsync()`  | Convert hold to committed allocation |
| TicketIssuer            | `IssueTicketsAsync()`       | Generate ticket records              |
| RealtimePublisher       | `PublishSeatChangesAsync()` | Notify clients                       |
| ReconciliationWorker    | `ReconcileAsync()`          | Resolve ambiguous payment states     |

### API contract design

| Endpoint                               | Method  | Result                          |
| -------------------------------------- | ------- | ------------------------------- |
| `/api/v1/events`                       | GET     | Event catalog                   |
| `/api/v1/events/{id}`                  | GET     | Event details                   |
| `/api/v1/events/{id}/seats`            | GET     | Availability snapshot           |
| `/api/v1/holds`                        | POST    | Create 5-minute hold            |
| `/api/v1/holds/{id}`                   | GET     | Hold details                    |
| `/api/v1/holds/{id}`                   | DELETE  | Release hold                    |
| `/api/v1/orders`                       | POST    | Create order from hold          |
| `/api/v1/orders/{id}`                  | GET     | Authoritative order status      |
| `/api/v1/payments/{orderId}/authorize` | POST    | Begin payment authorization     |
| `/api/v1/webhooks/payments`            | POST    | Verified payment event receiver |
| `/api/v1/tickets/me`                   | GET     | Customer tickets                |
| `/api/v1/tickets/check-in`             | POST    | Validate venue entry            |
| `/hubs/availability`                   | SignalR | Seat state changes              |

For mutation requests, use an `Idempotency-Key` header and a correlation ID. Use appropriate HTTP responses: `201` for new holds, `409` for seat conflicts, `410` for expired holds, `429` for rate limiting, and `503` for temporarily unavailable services.

## 4. Advanced database design and ERD

For ticketing, the database is not merely a storage system. It is the final enforcement boundary for seat exclusivity.


Additional infrastructure tables:

- `WebhookInbox`: payment event ID, payment reference, payload hash, processing status.
- `OutboxEvents`: event ID, aggregate ID, payload, creation date, publish status.
- `AuditLogs`: actor, operation, old/new state, time and correlation ID.
- `IdempotencyRequests`: request key, authenticated user, request fingerprint and previous result.
- `Refunds`: refund amount, reason, provider reference and settlement state.

### Critical SQL Server indexes

```
-- Primary seat lookup and exclusivity
PRIMARY KEY (EventId, SeatId)

-- Efficient discovery of currently held seats
CREATE INDEX IX_EventSeats_Expiry
ON dbo.EventSeats(HoldExpiresAtUtc)
INCLUDE (EventId, SeatId, HoldId)
WHERE Status = 'HELD';

-- A user must not accidentally create
-- duplicate holds from the same request
CREATE UNIQUE INDEX UX_Holds_Idempotency
ON dbo.Holds(UserId, ClientRequestId);

-- Only one active ticket for an event-seat
CREATE UNIQUE INDEX UX_Tickets_ActiveSeat
ON dbo.Tickets(EventId, SeatId)
WHERE Status = 'ACTIVE';

-- Deduplicate incoming payment events
CREATE UNIQUE INDEX UX_WebhookInbox_ProviderEvent
ON dbo.WebhookInbox(Provider, ProviderEventId);
```

These indexes assume the corresponding columns and tables already exist. Include foreign keys, check constraints, appropriate deletion restrictions, and indexes for order/payment lookups in the complete schema.

Money representation: Store monetary amounts in integer minor units where the currency uses minor units, or use an appropriate fixed-precision decimal model. Do not use floating-point numbers for ticket prices.

বাংলায়: `EventSeats` টেবিলের `(EventId, SeatId)` একটি ইউনিক পরিচয়। একই ইভেন্টের একই সিটের জন্য একটি authoritative inventory record থাকবে। ডাটাবেজে এই সিটটি `HELD`, `ALLOCATED` বা `SOLD` থাকলে অন্য ক্রেতা সেটি নিতে পারবে না।

## 5. Seat reservation, TTL and double-booking prevention

### State machine


The `ALLOCATED` state is deliberately different from `HELD`. Once the payment has been authorized and the allocation committed, the seat cannot automatically expire just because the original five-minute hold clock ended.

A separate reconciliation process must resolve payment failures before releasing an allocated seat.

### Interactive seat-selection prototype

This is a frontend-only demonstration of seat selection. It shows the UI states; it does not contact a database or place real holds.

## Live seat map — prototype

VIP section · Example event

Demo

STAGE

A1

A2

A3

A4

A5

A6Sold

B1

B2

B3

B4

B5

B6

C1

C2

C3

C4

C5Sold

C6

Available

Selected

Held

Sold

2 seats selected

B3, B4

## ৳3,000

Before fees

Simulate 5-minute holdMaximum 4 seats per booking in this example. No real timer or reservation is running.

### How the actual booking transaction works

Assume customers A and B both attempt to book seat `A12`.

Customer A

Requests A12 at the same moment.

Atomic claim succeeds.

Hold accepted

Customer B

Requests exactly the same A12.

Atomic claim finds it unavailable.

409 Seat conflict

### Atomic SQL Server implementation

The following is the core acquisition algorithm for `dbo.EventSeats`. It assumes the supporting `Holds`, `HoldSeats`, and outbox tables are defined as described above.

```
CREATE TYPE dbo.SeatIdList AS TABLE
(
    SeatId BIGINT NOT NULL PRIMARY KEY
);
GO

CREATE OR ALTER PROCEDURE dbo.TryHoldSeats
    @EventId BIGINT,
    @UserId BIGINT,
    @HoldId UNIQUEIDENTIFIER,
    @ClientRequestId UNIQUEIDENTIFIER,
    @SeatIds dbo.SeatIdList READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @Expires DATETIME2(3) =
        DATEADD(MINUTE, 5, @Now);

    DECLARE @Expected INT =
        (SELECT COUNT(*) FROM @SeatIds);

    IF @Expected < 1 OR @Expected > 4
        THROW 51000, 'Invalid seat count', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Unique (UserId, ClientRequestId)
        -- prevents duplicate logical requests.
        INSERT INTO dbo.Holds
        (
            HoldId, EventId, UserId,
            ClientRequestId, Status,
            CreatedAtUtc, ExpiresAtUtc
        )
        VALUES
        (
            @HoldId, @EventId, @UserId,
            @ClientRequestId, 'ACTIVE',
            @Now, @Expires
        );

        UPDATE es
        SET
            Status = 'HELD',
            HoldId = @HoldId,
            HoldExpiresAtUtc = @Expires
        FROM dbo.EventSeats es
        INNER JOIN @SeatIds s
            ON s.SeatId = es.SeatId
        WHERE es.EventId = @EventId
          AND
          (
              es.Status = 'AVAILABLE'
              OR
              (
                  es.Status = 'HELD'
                  AND es.HoldExpiresAtUtc <= @Now
              )
          );

        IF @@ROWCOUNT <> @Expected
            THROW 51001, 'Seats unavailable', 1;

        -- Snapshot authoritative prices.
        INSERT INTO dbo.HoldSeats
        (
            HoldId, EventId, SeatId, PriceMinor
        )
        SELECT
            @HoldId, es.EventId,
            es.SeatId, es.PriceMinor
        FROM dbo.EventSeats es
        INNER JOIN @SeatIds s
            ON s.SeatId = es.SeatId
        WHERE es.EventId = @EventId
          AND es.HoldId = @HoldId;

        -- Insert an OutboxEvents record here,
        -- in the same transaction.

        COMMIT TRANSACTION;

        SELECT @HoldId AS HoldId,
               @Expires AS ExpiresAtUtc;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH
END;
```

The seat updates, hold creation, seat-price snapshots and outbox insert must form one transaction.

In production, the application also needs to handle duplicate idempotency keys by returning the existing hold result, validate the booking policy and event status, and retry selected SQL deadlock victims (`1205`) with bounded jitter. Updating seats in a consistent order can further reduce deadlocks.

Why is this safe? Concurrent conditional updates cannot both successfully acquire the same authoritative row. If the requested number of rows is not updated, the entire transaction rolls back. No partial allocation of a requested seat group remains.

### TTL expiration architecture

The database clock, rather than the browser countdown, determines expiry.


Two protection layers are essential:

- Lazy expiry: A new booking is permitted to atomically reclaim a previously expired `HELD` seat, even if the cleanup worker has not run.
- Background sweep: A worker periodically marks expired holds and releases only seats still owned by those holds.

A late cleanup worker must never release a seat subsequently assigned to another hold. Therefore every release checks both the current `HoldId` and its status.

A Redis TTL can help schedule expiration, but Redis key expiry alone must never determine whether a seat is available.

## 6. Payment architecture and distributed consistency

The most dangerous booking scenario is not simply two users clicking the same seat. It is a payment succeeding while the booking system is crashing, timing out, or losing messages.

A database transaction cannot atomically commit changes inside SQL Server and an external payment provider. We therefore use a stateful payment saga with recovery and compensation.

### Recommended authorization-and-capture workflow


This assumes the selected payment method supports separate authorization and capture. Some providers and payment methods do not; Stripe's documentation explicitly distinguishes supported and unsupported methods.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://docs.stripe.com\&sz=32)

docs.stripe.com+1



For an immediate-capture payment method, use an alternative saga that either fulfills the still-valid reservation or initiates a refund if allocation is no longer possible.

### Payment and booking states

| State                         | Seat state         | Customer experience       |
| ----------------------------- | ------------------ | ------------------------- |
| Hold active                   | `HELD`             | Countdown and checkout    |
| Payment authorization ongoing | `HELD`             | Secure payment screen     |
| Authorization confirmed       | `ALLOCATED`        | Processing payment        |
| Capture processing            | `ALLOCATED`        | Finalizing booking        |
| Capture confirmed             | `SOLD`             | Booking confirmed         |
| Ticket issued                 | `SOLD`             | Ticket and QR available   |
| Definitive payment failure    | Released when safe | Retry or choose new seats |
| Payment outcome unknown       | `ALLOCATED`        | Verification in progress  |

Critical rule: Never automatically free an `ALLOCATED` seat after a short timeout if the external payment might already have been captured. First reconcile the payment with the provider and verify whether a void or refund is necessary.

### Recovery from real payment failures

Case A — Customer's internet disconnects after payment

Do not immediately ask the customer to pay again. Retrieve the existing order and payment attempt, then check or reconcile the provider status.

Case B — Hold expires before authorization

Reject the allocation, void the authorization if possible, and never take a seat from another booking.

Case C — Capture succeeds, but the worker crashes

Retry or reconcile using the same payment attempt and idempotency key. Do not initiate a second independent charge.

Case D — Webhook delivered multiple times

Deduplicate in the webhook inbox, validate the current state, and make the transition idempotent.

Payment providers may deliver duplicate or out-of-order events. Stripe documents both behaviors and recommends asynchronous processing and event deduplication. Its idempotency support helps make payment operations retry-safe.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://docs.stripe.com\&sz=32)

docs.stripe.com+1



### Transactional Outbox + Inbox pattern


The outbox ensures that a booking state change and its intended outgoing event are committed together.

The publisher can deliver an event more than once, so consumers must be idempotent. We do not assume end-to-end exactly-once message delivery.

For every payment attempt, store the provider reference, expected amount, currency, status, and last reconciliation result. A scheduled reconciliation worker examines ambiguous or delayed transactions.

## 7. Real-time data flow and seat synchronization

Live availability improves the booking experience, but a real-time connection must not determine the final ownership of a seat.

### Live seat event example

```
{
  "eventType": "seat.status.changed",
  "eventId": 501,
  "seatId": 12012,
  "status": "HELD",
  "expiresAtUtc": "2026-11-18T06:35:00Z",
  "seatVersion": "00000000000009AF",
  "occurredAtUtc": "2026-11-18T06:30:00Z"
}
```

The timestamp is illustrative. The `seatVersion` lets the client ignore stale changes to the same seat.

### Real data flow

1. Customer A opens the event and receives an availability snapshot from the read API.
2. Customer B successfully holds seat A12. SQL Server commits the new inventory state.
3. The transactional outbox publishes an inventory change to RabbitMQ.
4. The real-time service broadcasts a compact delta to subscribers of event 501.
5. Customer A sees A12 become unavailable without a full page reload.
6. If Customer A clicks A12 using a stale snapshot, the booking API still rejects the conflicting claim.

At high concurrency, avoid broadcasting complete seat maps for every individual mutation. Instead, use small per-seat or batched changes, grouped by event or venue section.

SignalR can scale through Azure SignalR Service or a Redis backplane. For self-hosted Redis-backed SignalR, account for session affinity requirements and WebSocket connection resources.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://learn.microsoft.com\&sz=32)

Microsoft Learn+1



### Connection recovery

When the WebSocket disconnects:

1. Show a subtle Reconnecting live availability status.
2. Preserve the user's selected seat IDs without falsely claiming they are reserved.
3. Reconnect with bounded exponential backoff and jitter.
4. Fetch a fresh authoritative snapshot after reconnection, then resume changes.
5. Recheck the server-side hold before allowing payment.

A user's personal hold information should travel through an authenticated private channel, not a public event-wide broadcast.

## 8. UI/UX architecture and smooth animation

Design direction: fast, high-clarity, responsive ticketing interface

[Gigsy — Event Ticketing App by Nija Design for Nija Works on Dribbble](https://images.openai.com/static-rsc-4/yWFI_DvoFLNRvYRtUM_G-iy_JodHFSjJvTO8Brq4qrZHFTqS2Mi5sWroeNdt02GOTgTEfHXJhOZpVpTPQLJLjYGYnXo3bOVSgrhdj3YmpW5P2t804AHgMkD6MahklPyPShu3jvt6TERghUDv1gXDk5hoPRlug9UQxs1aa8_LBf0?purpose=inline)

Event discovery and detail page

Search, event information, venue, date, pricing and booking CTA

[Concert Booking App Mobile UI Design Focusing on Accessibility by Sameer Acharya on Dribbble](https://images.openai.com/static-rsc-4/uQ2AvGRcF8x7qKgIZYPz4Hp_vLbhvTIY52UUvXKQUI1B9qSoMpaZ_aBQl4D_8USxdiWuYbBb7_Dara2MtA8-66LehXBkeVb_pulCXkjrZcRckghaFJQRPWt4bgO5Ryy1OpqzdRIqTcUZWqawdQ_OtG67-_m9lRkvljNC2-MqTOE?purpose=inline)

Interactive venue seat map

Accessible seat states, zoom, reservation countdown and price summary

Visual references are conceptual design inspiration, not screenshots of the proposed application.

### Recommended screen architecture

| Screen              | Essential UX features                                             |
| ------------------- | ----------------------------------------------------------------- |
| Event discovery     | Search, city, category, date, price, availability                 |
| Event details       | Performer, schedule, venue, policies, interactive venue preview   |
| Waiting room        | Queue position, progress, entry status                            |
| Seat selection      | Pan/zoom, section pricing, keyboard navigation, seat state legend |
| Checkout            | Expiration clock, fee transparency, secure payment                |
| Processing          | Persistent order state, safe retry instructions                   |
| Booking success     | Order ID, QR ticket, invoice, transfer rules                      |
| My tickets          | Active tickets, event reminders, order history                    |
| Organizer dashboard | Sales, occupancy, pricing, reconciliation                         |
| Admin operations    | Payment exceptions, refund approvals, audit trails                |

### Customer checkout wireframe

```
┌────────────────────────────────────┐
│ EventHub                  Profile  │
├────────────────────────────────────┤
│ SUMMER MUSIC FESTIVAL              │
│ Venue · Date · Section VIP         │
│                                    │
│     ┌────────────────────┐         │
│     │       STAGE        │         │
│     └────────────────────┘         │
│                                    │
│    A1  A2  A3  A4  A5  A6          │
│    B1  B2  B3  B4  B5  B6          │
│    C1  C2  C3  C4  C5  C6          │
│                                    │
│  Selected: B3, B4                  │
│                                    │
│  Seat subtotal        ৳3,000      │
│  Service fees          ৳150       │
│  Total                ৳3,150      │
│                                    │
│  Hold expires in       04:59      │
│                                    │
│     [ Continue to payment ]        │
└────────────────────────────────────┘
```

This is illustrative pricing; actual totals should come from an immutable server-generated quote associated with the hold.

### Animation specifications

| Interaction           | Animation                    | Suggested duration |
| --------------------- | ---------------------------- | ------------------ |
| Seat selected         | Scale + state transition     | 120–180 ms         |
| Seat rejected         | Brief shake + error message  | 180–250 ms         |
| Seat map opening      | Opacity + subtle translation | 180–240 ms         |
| Checkout drawer       | Slide + fade                 | 200–300 ms         |
| Loading seats         | Skeleton loading             | Until data arrives |
| Booking confirmed     | Checkmark reveal             | 300–450 ms         |
| Price changes         | Smooth numeric transition    | 150–250 ms         |
| Countdown near expiry | Subtle emphasis, no flashing | State-driven       |

These timings are design recommendations, not guarantees.

Animations must remain lightweight. Prefer `transform` and `opacity` over animating width, height, or layout-heavy properties. Respect the operating system's `prefers-reduced-motion` setting.

### Premium UI/UX considerations

- Use canvas or optimized SVG for large seating charts instead of rendering thousands of complex React elements without virtualization.
- Keep selections visibly distinct from server-confirmed holds.
- Do not communicate seat status by color alone; add labels, icons, and accessible descriptions.
- Support keyboard selection and screen-reader announcements for availability changes.
- Provide a textual seat list for customers unable to use a spatial seat map.
- Maintain tap targets around 44×44 CSS pixels where practical.
- Show a transparent fee breakdown before payment.
- Never show Booking confirmed until the payment and ticketing workflow actually reaches the confirmed state.

বাংলায়: সুন্দর Animation গুরুত্বপূর্ণ, কিন্তু Booking System-এর মূল লক্ষ্য হচ্ছে দ্রুত কাজ করা এবং সঠিক তথ্য দেখানো। সিট সিলেক্ট করলে animation দেখা যাবে, কিন্তু সার্ভার থেকে নিশ্চিত না হওয়া পর্যন্ত সেটিকে বুকড বলা যাবে না।

## 9. Performance engineering and system lag prevention

For a popular ticket release, the main performance risk is a sudden concentration of traffic around a small number of events.

### Proposed latency budgets

Example service-level objectives under admitted peak load, to be validated by testing

| Operation                  | Target p95 | Approach                     |
| -------------------------- | ---------- | ---------------------------- |
| Event discovery            | 200 ms     | CDN, search and caching      |
| Seat availability read     | 300 ms     | Optimized read projection    |
| Hold creation              | 500 ms     | Short SQL transaction        |
| Order creation             | 350 ms     | Minimal synchronous work     |
| Live state delivery        | 1,000 ms   | Batched SignalR deltas       |
| Initial page interactivity | 2.5 sec    | Streaming and code splitting |

Payment completion depends on the external payment method and cannot be guaranteed within an API latency budget.

### Why uncontrolled polling is dangerous

Consider 100,000 concurrent customers refreshing the seating chart every two seconds.

ILLUSTRATIVE TRAFFIC CALCULATION

# 50,000

HTTP reads per second with 2-second polling

# 100,000

Active clients receiving event-driven updates

Improvement: Fetch an initial snapshot, then deliver seat changes through live subscriptions. Measure WebSocket fanout and reconnect storms separately.

### Performance strategy by component

| Bottleneck                | Root cause                      | Recommended solution                      |
| ------------------------- | ------------------------------- | ----------------------------------------- |
| Database lock waits       | Hot seat contention             | Conditional writes, short transactions    |
| SQL deadlocks             | Competing multi-seat operations | Consistent lock order and bounded retries |
| API request spikes        | Popular event launches          | Waiting room and admission control        |
| Cache misses              | Excessive event queries         | Cache warming, TTL and request coalescing |
| WebSocket pressure        | Too many subscriptions          | Grouping, batching, scale-out             |
| Slow seat map             | Thousands of React nodes        | SVG/canvas, viewport culling              |
| Browser input lag         | Excessive rerenders             | Memoization, localized state              |
| Queue buildup             | Worker capacity shortage        | Scale consumers, backpressure, DLQ        |
| Payment delays            | External provider latency       | Async workflows and reconciliation        |
| SQL connection exhaustion | Too many application requests   | Pool limits and admission control         |

### Waiting room architecture


The waiting room protects the booking service during exceptionally popular sales.

A production queue should use signed, expiring admission tokens and server-side enforcement. Rate limits must apply to identities and relevant request classes rather than trusting browser sessions alone.

For sales advertised as fair or randomized, admission ordering must implement that published policy. Redis queues alone do not provide a complete durable fairness guarantee.

### Database scaling strategy

Use the following sequence rather than adding shards prematurely:

1. Optimize seat lookup indexes and SQL execution plans.
2. Use read projections or replicas for non-authoritative discovery queries.
3. Enable and evaluate row-versioned reads where appropriate.
4. Separate analytics from transactional booking workloads.
5. Partition operational data for manageability and retention.
6. If necessary, shard inventory ownership by `EventId`, keeping all inventory for one event under one authoritative write owner.

For general admission events with 5,000 interchangeable tickets rather than assigned seats, use a separate atomic capacity-allocation model. Do not create fake individual seat assignments just to reuse the seating API.

The critical invariant becomes:

\\[ \text{Confirmed}+\text{ActiveHolds}+\text{Allocated} \leq \text{SellableCapacity} \\]

When active holds expire, they cease to count as active reservations according to the authoritative allocation logic.

## 10. Advanced error handling and bug prevention

### Production error catalog

| Problem                     | Backend response                     | Customer UI                             |
| --------------------------- | ------------------------------------ | --------------------------------------- |
| Another user wins seat      | `409 SEAT_UNAVAILABLE`               | Suggest available alternatives          |
| Hold expired                | `410 HOLD_EXPIRED`                   | Clear hold and offer reselection        |
| Payment declined            | `402 PAYMENT_DECLINED`               | Offer another payment method            |
| Duplicate booking request   | Previous idempotent result           | Resume existing booking                 |
| Rate limit reached          | `429 RATE_LIMITED`                   | Explain retry instructions              |
| SQL temporarily unavailable | `503 BOOKING_UNAVAILABLE`            | Preserve selection, do not claim a hold |
| Payment status unknown      | Order remains processing             | Show status verification                |
| Ticket generation delayed   | Order confirmed, fulfillment pending | Display order receipt and progress      |

### Standard error contract

```
{
  "type": "https://api.example.com/problems/seat-unavailable",
  "title": "Selected seat is unavailable",
  "status": 409,
  "code": "SEAT_UNAVAILABLE",
  "correlationId": "01J-EXAMPLE-7342",
  "retryable": false,
  "details": {
    "eventId": 501,
    "unavailableSeatIds": [12012]
  }
}
```

Use structured logs with correlation IDs. Never expose database stack traces, payment credentials, or sensitive provider data in public responses.

### Common critical bugs

Bug 1 — Check then insert

Two customers both read `AVAILABLE` before either updates the row. Fix with a single transactional conditional claim.

Bug 2 — Browser countdown determines expiry

Client clocks are unreliable and can be manipulated. Fix with server-authoritative UTC expiration and final checks.

Bug 3 — Duplicate payment webhook creates duplicate tickets

Fix with webhook inbox deduplication, valid state transitions, and a unique active-ticket constraint.

Bug 4 — Background expiry releases a newly acquired seat

Fix by checking hold ownership and status in the release transaction.

Bug 5 — Network timeout triggers a second payment

Fix by reusing the logical payment attempt and reconciling the provider state before retrying.

## 11. Security, ticket fraud and venue check-in

Ticket platforms must protect customers, payment operations and event access.

### Security controls

- Authentication using OIDC/OAuth-based identity and appropriately scoped access tokens.
- Authorization distinguishing customer, organizer, venue staff and platform administrator.
- Server-side price validation and signed admission tokens.
- Bot detection, booking limits, abuse monitoring and risk-based challenges.
- Strict webhook signature verification and replay protection.
- Sensitive credentials stored in a secrets manager.
- Payment card information handled by an appropriate compliant payment provider rather than stored in the booking service.
- Audit logs for refunds, cancellations, ticket transfers and administrator actions.

### Secure QR ticket design

Do not trust a QR code containing only a predictable numeric ticket ID.

Prefer an opaque or signed token tied to a ticket, event, validity period and revocation state.


The check-in operation must atomically transition a ticket from unscanned to checked in. Two simultaneous scans of the same ticket must not both succeed.

Offline gate scanning requires a separate architecture for signed credential validation, local duplicate detection and cross-device reconciliation. A fully offline gate cannot provide a global no-duplicate-entry guarantee across disconnected devices without additional operational controls.

## 12. Observability, availability and disaster recovery

A production ticketing system needs visibility into the entire user journey.

## Operations dashboard — proposed monitoring layout

Active Holds

## 1,240

Confirmed Orders

## 8,150

Hold API p95

## 210 ms

Pending Reconciliation

## 12

Illustrative dashboard values only — not connected to a live service.

### Essential production metrics

| Metric                           | Operational significance     |
| -------------------------------- | ---------------------------- |
| `hold_attempts_total`            | Booking demand               |
| `hold_conflicts_total`           | Seat contention              |
| `hold_latency_p95`               | Booking responsiveness       |
| `sql_deadlocks_total`            | Concurrency pressure         |
| `payment_reconciliation_pending` | Financial uncertainty        |
| `allocated_seats_age_seconds`    | Potentially stuck allocation |
| `outbox_oldest_unpublished_age`  | Event publication lag        |
| `websocket_delivery_lag`         | Live availability freshness  |
| `duplicate_active_seats`         | Critical integrity violation |
| `tickets_pending_issuance`       | Fulfillment backlog          |

A seat conflict caused by another buyer is normal business behavior, not necessarily a system incident. However, any confirmed duplicate sale should trigger a critical incident.

For database availability, use a SQL Server high-availability configuration appropriate to the deployed edition and infrastructure, with tested backups, restore procedures and failover drills.

Example initial recovery objectives:

- RTO: 15 minutes for a major availability failure.
- RPO: Zero committed booking loss for synchronous local high availability, subject to actual configuration and failover guarantees.
- Disaster recovery: Separate RPO/RTO targets based on regional replication and tested recovery capabilities.

Do not silently accept new bookings during a period in which the authoritative database cannot provide safe write guarantees.

## 13. Quality assurance and high-concurrency testing

The most important acceptance test is a race between many customers for the same seat.

### Test scenarios

| Test                                | Expected result                              |
| ----------------------------------- | -------------------------------------------- |
| 10,000 users try one seat           | Exactly one active successful hold at a time |
| Two users hold overlapping groups   | No partial group allocation                  |
| Hold expires during checkout        | Expired claim cannot be confirmed            |
| Worker runs after hold was replaced | New owner's seat unaffected                  |
| Webhook delivered 20 times          | One logical payment outcome                  |
| Payment capture timeout             | No unauthorized duplicate capture            |
| SQL crashes during transaction      | Either complete commit or rollback           |
| Redis fails                         | Authoritative booking remains safe           |
| RabbitMQ unavailable                | Outbox safely retains unpublished events     |
| Ticket scanned twice concurrently   | Only one successful check-in                 |

### Concurrency-test pseudocode

```
// Test concept: 10,000 competing requests.
// Use isolated test users and idempotency keys.

var results = await Task.WhenAll(
    Enumerable.Range(0, 10000).Select(
        async i => await bookingClient.TryHoldAsync(
            eventId: 501,
            seatIds: [12012],
            clientRequestId: Guid.NewGuid()
        )
    )
);

var accepted = results.Count(r => r.IsSuccess);

Assert.Equal(1, accepted);
```

This is test pseudocode rather than a standalone runnable test. The actual test client must use independently authenticated eligible users, invoke the real API and verify final database state.

Also test that no more than one active ticket exists for the event-seat after payment completion. An HTTP success count alone is not sufficient evidence of booking correctness.

For repeatable performance testing, use k6 or another load-testing system and inject failures with a proxy such as Toxiproxy. Test lock waits, deadlocks, timeouts, stale cache entries, delayed payment events and consumer restarts.

## 14. Step-by-step development roadmap

Suggested 12-week engineering plan; estimates depend on team size and scope.

1. Weeks 1–2: Architecture and product foundation

   Requirements, domain model, role permissions, HLD, LLD, ERD, frontend design system, API contracts and development infrastructure.
2. Weeks 3–4: Event catalog and seat inventory

   Venue seating plans, event management, seat pricing, SQL migrations, caching, and authoritative seat availability.
3. Weeks 5–6: Holds and concurrency control

   Atomic multi-seat booking, expiry, idempotency, conflict responses, waiting-room integration and concurrency tests.
4. Weeks 7–8: Orders and payment orchestration

   Payment adapters, authorization, capture, webhooks, transactional outbox/inbox, compensation and reconciliation.
5. Weeks 9–10: Real-time UX and ticket fulfillment

   SignalR, responsive seating maps, checkout UI, ticket issuance, QR validation, animations and notifications.
6. Weeks 11–12: Reliability and release readiness

   Load testing, performance tuning, security testing, monitoring, recovery exercises, CI/CD and operational runbooks.

### Engineering completion criteria

The system should not be considered production-ready until these conditions are demonstrated:

- No double-sales under concurrency and injected failures.
- Reliable and auditable payment-to-order reconciliation.
- No permanent inventory loss from expired holds.
- Safe payment retries and duplicate webhook handling.
- Recovery of pending allocations after worker failure.
- Accessible and responsive customer booking flows.
- Operational monitoring, alerts, backups and tested recovery procedures.
- Load test results meeting agreed performance targets.

## 15. Final architecture recommendation

For this system, I recommend ASP.NET Core .NET 10 + SQL Server + Redis + RabbitMQ + SignalR + Next.js.

Build seat inventory and holds around one strongly consistent transactional boundary. Keep Redis responsible for acceleration, RabbitMQ for background workflow delivery, and SignalR for user-facing state changes. Use a recoverable payment saga and make all ticket issuance and check-in actions idempotent.

The most important engineering principle is:

A fast booking experience can tolerate slightly delayed visual updates. It must never tolerate inconsistent ownership of a seat or an unaccounted-for customer payment.

বাংলা সারসংক্ষেপ: এই প্রজেক্টের সবচেয়ে গুরুত্বপূর্ণ অংশ হচ্ছে Atomic Seat Reservation, Double-Booking Prevention, Payment Recovery, Real-Time Synchronization এবং High-Concurrency Performance। UI/UX, animation এবং দ্রুত response গুরুত্বপূর্ণ হলেও SQL Server-এর transactional consistency ও payment reconciliation-এর নিরাপত্তা সর্বোচ্চ অগ্রাধিকার পাবে।

This blueprint provides the architecture and core implementation patterns. The full application, executable multi-service source code, migrations, deployment manifests and benchmark results would still need to be built and validated.