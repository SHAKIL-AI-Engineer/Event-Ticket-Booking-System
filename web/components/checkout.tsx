"use client";
import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  Timer,
  ShieldCheck,
  Ticket,
  LockKeyhole,
  Loader2,
  CheckCircle2,
  CalendarDays,
  MapPin,
  Download,
  Clock3,
  AlertCircle,
  Receipt,
  RefreshCw,
  CreditCard,
} from "lucide-react";
import { api, date, json, money, time, utc } from "@/lib/api";
import type { EventItem, Hold, Order, OrderView } from "@/lib/types";
import { useApp } from "./providers";
import {
  Badge,
  Breadcrumb,
  ErrorState,
  Field,
  Guard,
  Loading,
  Modal,
  Steps,
} from "./ui";
export function Checkout({ id }: { id: string }) {
  return (
    <Guard>
      <CheckoutContent id={id} />
    </Guard>
  );
}
function CheckoutContent({ id }: { id: string }) {
  const router = useRouter(),
    { user, config, notify } = useApp(),
    [hold, setHold] = useState<Hold>(),
    [event, setEvent] = useState<EventItem>(),
    [error, setError] = useState(""),
    [seconds, setSeconds] = useState(300),
    [outcome, setOutcome] = useState("success"),
    [terms, setTerms] = useState(false),
    [busy, setBusy] = useState(false),
    [orderId, setOrderId] = useState(""),
    [clientSecret, setClientSecret] = useState(""),
    offset = useRef(0);
  useEffect(() => {
    api<Hold>("/holds/" + id)
      .then(async (h) => {
        setHold(h);
        offset.current = new Date(utc(h.serverTime)).getTime() - Date.now();
        setEvent(await api<EventItem>("/events/" + h.eventId));
      })
      .catch((e) => setError(e.message));
  }, [id]);
  useEffect(() => {
    if (!hold) return;
    const tick = () =>
      setSeconds(
        Math.max(
          0,
          Math.ceil(
            (new Date(utc(hold.expiresAt)).getTime() -
              Date.now() -
              offset.current) /
              1000,
          ),
        ),
      );
    tick();
    const timer = setInterval(tick, 1000);
    return () => clearInterval(timer);
  }, [hold]);
  async function pay() {
    if (!hold || !terms) return;
    setBusy(true);
    let created = orderId;
    try {
      if (!created) {
        const order = await api<Order>("/orders", {
          method: "POST",
          headers: { "Idempotency-Key": "order-" + id },
          body: json({ holdId: id }),
        });
        created = order.id;
        setOrderId(created);
      }
      const result = await api<OrderView>(`/payments/${created}/authorize`, {
        method: "POST",
        headers: { "Idempotency-Key": "pay-" + created },
        body: json({ outcome }),
      });
      if (
        result.payment?.clientSecret &&
        result.payment.status === "REQUIRES_ACTION"
      )
        setClientSecret(result.payment.clientSecret);
      else router.push("/orders/" + created);
    } catch (e) {
      notify((e as Error).message, true);
      if (created) router.push("/orders/" + created);
    } finally {
      setBusy(false);
    }
  }
  async function release() {
    setBusy(true);
    try {
      await api("/holds/" + id, { method: "DELETE" });
      router.push("/events/" + hold?.eventId + "/seats");
    } catch (e) {
      notify((e as Error).message, true);
      setBusy(false);
    }
  }
  if (error)
    return (
      <div className="container page">
        <ErrorState message={error} />
        <Link className="button" href="/events">
          Find an event
        </Link>
      </div>
    );
  if (!hold || !event) return <Loading text="Keeping your seats warm…" />;
  const expired =
    seconds === 0 || ["EXPIRED", "RELEASED"].includes(hold.status);
  if (hold.status === "CONVERTED" || hold.status === "ALLOCATED")
    return (
      <div className="container page">
        <h1>Your order is in progress.</h1>
        <p>You can find the latest status in your order history.</p>
        <Link className="button" href="/tickets?tab=orders">
          View my orders
        </Link>
      </div>
    );
  return (
    <div className="container page checkout-page">
      <Breadcrumb
        items={[
          { label: "Discover events", href: "/events" },
          { label: event.ev.title, href: "/events/" + event.ev.id },
          { label: "Checkout" },
        ]}
      />
      <Steps current={1} />
      <div className="page-title">
        <span className="eyebrow">ALMOST PART OF THE STORY</span>
        <h1>Let’s make it official.</h1>
        <p>A few details, and your next great plan is set.</p>
      </div>
      <div
        className={`hold-timer ${seconds < 60 ? "urgent" : ""}`}
        role="timer"
      >
        <Timer size={22} />
        <div>
          <b>
            {expired
              ? "Your seat hold has expired"
              : "Your seats are saved for you"}
          </b>
          <span>
            {expired
              ? "Choose your seats again to continue."
              : "Complete your booking before the timer runs out."}
          </span>
        </div>
        <strong>
          {Math.floor(seconds / 60)
            .toString()
            .padStart(2, "0")}
          :{(seconds % 60).toString().padStart(2, "0")}
        </strong>
      </div>
      {expired ? (
        <div className="panel empty-state">
          <Clock3 size={36} />
          <h2>Good moments are still waiting.</h2>
          <p>
            Your seats have been released. Choose available seats to start a new
            booking.
          </p>
          <Link className="button" href={`/events/${event.ev.id}/seats`}>
            Choose seats again
          </Link>
        </div>
      ) : (
        <div className="checkout-layout">
          <div>
            <section className="panel checkout-section">
              <div className="section-number">
                <span>01</span>
                <h2>Who’s coming?</h2>
              </div>
              <p>
                Your tickets and booking updates will be available in your
                account.
              </p>
              <div className="contact-grid">
                <Field label="Full name">
                  <input value={user?.name || ""} readOnly />
                </Field>
                <Field label="Email address">
                  <input value={user?.email || ""} readOnly />
                </Field>
              </div>
              <Link className="text-link" href="/account">
                Update account details
              </Link>
            </section>
            <section className="panel checkout-section">
              <div className="section-number">
                <span>02</span>
                <h2>A little peace of mind</h2>
              </div>
              <div className="trust-feature">
                <ShieldCheck />
                <div>
                  <b>Your seats, guaranteed together</b>
                  <p>All selected seats are reserved in one booking.</p>
                </div>
              </div>
              <div className="trust-feature">
                <Ticket />
                <div>
                  <b>Ready when you are</b>
                  <p>Your QR tickets will appear after payment is confirmed.</p>
                </div>
              </div>
            </section>
            <section className="panel checkout-section">
              <div className="section-number">
                <span>03</span>
                <h2>Secure payment</h2>
              </div>
              {config?.paymentProvider === "Sandbox" ? (
                <>
                  <div className="sandbox-note">
                    <InfoIcon />
                    <div>
                      <b>Sandbox checkout</b>
                      <p>
                        No money is charged. Choose a payment scenario to
                        explore the complete booking experience.
                      </p>
                    </div>
                  </div>
                  <Field label="Payment scenario">
                    <select
                      value={outcome}
                      onChange={(e) => setOutcome(e.target.value)}
                      disabled={busy || !!orderId}
                    >
                      <option value="success">Successful payment</option>
                      <option value="decline">Declined payment</option>
                      <option value="delayed">
                        Delayed confirmation (about 8 seconds)
                      </option>
                    </select>
                  </Field>
                </>
              ) : (
                <p>
                  <LockKeyhole size={17} /> Payment details are handled securely
                  by Stripe. Card details never touch EventHub’s servers.
                </p>
              )}
              <label className="checkbox-field">
                <input
                  type="checkbox"
                  checked={terms}
                  onChange={(e) => setTerms(e.target.checked)}
                />
                <span>
                  I agree to the{" "}
                  <Link href="/policies" target="_blank">
                    ticket and refund policies
                  </Link>
                  . I’ve checked my event and seat details.
                </span>
              </label>
              {clientSecret && config?.stripePublishableKey ? (
                <StripePayment
                  secret={clientSecret}
                  publishableKey={config.stripePublishableKey}
                  orderId={orderId}
                />
              ) : clientSecret ? (
                <p className="form-error" role="alert">
                  Stripe’s publishable key is missing. Contact the administrator
                  to finish payment setup.
                </p>
              ) : (
                <button
                  className="button button-full pay-button"
                  onClick={pay}
                  disabled={!terms || busy}
                >
                  {busy ? (
                    <>
                      <Loader2 className="spin" size={18} /> Preparing your
                      booking…
                    </>
                  ) : (
                    <>
                      <LockKeyhole size={17} />
                      {config?.paymentProvider === "Sandbox"
                        ? "Confirm sandbox payment"
                        : "Continue to secure payment"}{" "}
                      · {money(hold.total)}
                    </>
                  )}
                </button>
              )}
              <span className="booking-secure">
                <ShieldCheck size={14} /> Encrypted checkout · Transparent
                pricing
              </span>
            </section>
          </div>
          <aside>
            <div className="panel order-summary">
              <h2>Your next great night</h2>
              <img src={event.ev.image} alt={event.ev.title} />
              <span className="eyebrow">{event.ev.category}</span>
              <h3>{event.ev.title}</h3>
              <p>
                <CalendarDays size={16} />
                {date(event.ev.startsAt)} · {time(event.ev.startsAt)} BST
              </p>
              <p>
                <MapPin size={16} />
                {event.venue.name}
              </p>
              <div className="summary-seats">
                {hold.seats.map((s) => (
                  <div key={s.id}>
                    <span>
                      <Ticket size={15} />
                      {s.section} · {s.row}
                      {s.number}
                    </span>
                    <b>{money(s.price)}</b>
                  </div>
                ))}
              </div>
              <div className="price-breakdown">
                <p>
                  <span>Ticket subtotal</span>
                  <b>{money(hold.subtotal)}</b>
                </p>
                <p>
                  <span>Service fee (5%)</span>
                  <b>{money(hold.fee)}</b>
                </p>
                <p className="price-total">
                  <span>Total</span>
                  <strong>{money(hold.total)}</strong>
                </p>
              </div>
              <button
                className="text-link"
                onClick={release}
                disabled={busy || !!clientSecret}
              >
                Change seats
              </button>
            </div>
          </aside>
        </div>
      )}
    </div>
  );
}
function InfoIcon() {
  return <AlertCircle size={22} />;
}
type StripeElement = {
  mount: (element: HTMLElement) => void;
  unmount: () => void;
};
type StripeElements = { create: (type: string) => StripeElement };
type StripeInstance = {
  elements: (options: {
    clientSecret: string;
    appearance: object;
  }) => StripeElements;
  confirmPayment: (options: {
    elements: StripeElements;
    confirmParams: { return_url: string };
    redirect: "if_required";
  }) => Promise<{ error?: { message: string } }>;
};
declare global {
  interface Window {
    Stripe?: (key: string) => StripeInstance;
  }
}
function StripePayment({
  secret,
  publishableKey,
  orderId,
}: {
  secret: string;
  publishableKey: string;
  orderId: string;
}) {
  const ref = useRef<HTMLDivElement>(null),
    stripe = useRef<StripeInstance | null>(null),
    elements = useRef<StripeElements | null>(null),
    [ready, setReady] = useState(false),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false),
    router = useRouter();
  useEffect(() => {
    let stopped = false;
    let element: StripeElement | undefined;
    function setup() {
      if (stopped || !window.Stripe || !ref.current) return;
      stripe.current = window.Stripe(publishableKey);
      elements.current = stripe.current.elements({
        clientSecret: secret,
        appearance: {
          theme: "stripe",
          variables: { colorPrimary: "#204e3c", borderRadius: "10px" },
        },
      });
      element = elements.current.create("payment");
      element.mount(ref.current);
      setReady(true);
    }
    if (window.Stripe) setup();
    else {
      const script = document.createElement("script");
      script.src = "https://js.stripe.com/v3/";
      script.onload = setup;
      script.onerror = () =>
        setError(
          "Unable to load secure payment fields. Please check your connection.",
        );
      document.body.appendChild(script);
    }
    return () => {
      stopped = true;
      element?.unmount();
    };
  }, [secret, publishableKey]);
  async function confirm() {
    if (!stripe.current || !elements.current) return;
    setBusy(true);
    const result = await stripe.current.confirmPayment({
      elements: elements.current,
      confirmParams: { return_url: location.origin + "/orders/" + orderId },
      redirect: "if_required",
    });
    if (result.error) {
      setError(result.error.message);
      setBusy(false);
    } else router.push("/orders/" + orderId);
  }
  return (
    <div>
      <div ref={ref} />
      {error && (
        <p role="alert" className="form-error">
          {error}
        </p>
      )}
      <button
        className="button button-full"
        disabled={!ready || busy}
        onClick={confirm}
      >
        {busy ? (
          <Loader2 className="spin" size={18} />
        ) : (
          <LockKeyhole size={18} />
        )}{" "}
        Confirm payment
      </button>
    </div>
  );
}
export function OrderPage({ id }: { id: string }) {
  return (
    <Guard>
      <OrderContent id={id} />
    </Guard>
  );
}
function OrderContent({ id }: { id: string }) {
  const { notify } = useApp(),
    [view, setView] = useState<OrderView>(),
    [error, setError] = useState(""),
    [refund, setRefund] = useState(false),
    [reason, setReason] = useState(""),
    [busy, setBusy] = useState(false);
  const load = useCallback(
    () => api<OrderView>("/orders/" + id).then(setView),
    [id],
  );
  useEffect(() => {
    void load().catch((e) => setError(e.message));
  }, [load]);
  const state = view?.order.status;
  useEffect(() => {
    if (!state || !["PROCESSING", "PENDING"].includes(state)) return;
    const timer = setInterval(() => {
      void load().catch(() => {});
    }, 3000);
    return () => clearInterval(timer);
  }, [state, load]);
  async function reconcile() {
    setBusy(true);
    try {
      await api("/orders/" + id + "/reconcile", { method: "POST" });
      await load();
      notify("Order status updated.");
    } catch (e) {
      notify((e as Error).message, true);
    } finally {
      setBusy(false);
    }
  }
  async function requestRefund(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      await api("/orders/" + id + "/refund", {
        method: "POST",
        body: json({ reason }),
      });
      setRefund(false);
      await load();
      notify("Your refund request has been sent for review.");
    } catch (e) {
      notify((e as Error).message, true);
    } finally {
      setBusy(false);
    }
  }
  if (error)
    return (
      <div className="container page">
        <ErrorState
          message={error}
          retry={() => load().catch((e) => setError(e.message))}
        />
      </div>
    );
  if (!view) return <Loading text="Checking your booking…" />;
  const { order, ev, venue, payment } = view,
    confirmed = order.status === "CONFIRMED",
    processing = ["PROCESSING", "PENDING"].includes(order.status),
    failed = ["FAILED", "EXPIRED", "CANCELLED"].includes(order.status);
  return (
    <div className="container page order-page">
      <Steps current={confirmed ? 2 : 1} />
      <div className="order-outcome">
        <span className={`outcome-icon ${failed ? "failed" : ""}`}>
          {confirmed ? (
            <CheckCircle2 size={40} />
          ) : processing ? (
            <Loader2 className="spin" size={40} />
          ) : (
            <Receipt size={40} />
          )}
        </span>
        <span className="eyebrow">
          {confirmed
            ? "MAKE ROOM FOR A GREAT MEMORY"
            : processing
              ? "WE’RE ON IT"
              : "YOUR BOOKING UPDATE"}
        </span>
        <h1>
          {confirmed
            ? "You’re going. Let it sink in."
            : processing
              ? "A great moment is in the making."
              : order.status === "REFUNDED"
                ? "Your refund is confirmed."
                : order.status === "REFUND_REQUESTED"
                  ? "Your refund is being reviewed."
                  : "Let’s find your next moment."}
        </h1>
        <p>
          {confirmed
            ? "Your booking is confirmed and your tickets are ready. See you there."
            : processing
              ? "We’re verifying the payment and your seats. You can safely leave this page and return from My tickets."
              : payment?.error ||
                "The latest status of your booking is shown below."}
        </p>
        {payment?.provider === "Sandbox" && (
          <span className="sandbox-chip">
            Sandbox booking · No money was charged
          </span>
        )}
      </div>
      <div className="confirmation-card panel">
        <img src={ev.image} alt={ev.title} />
        <div>
          <Badge status={order.status} />
          <h2>{ev.title}</h2>
          <p>
            <CalendarDays size={17} />
            {date(ev.startsAt)} · {time(ev.startsAt)} BST
          </p>
          <p>
            <MapPin size={17} />
            {venue.name}, {venue.city}
          </p>
          <div className="ticket-seat-chips">
            {view.seats.map((s) => (
              <span key={s.id}>
                {s.section} · {s.row}
                {s.number}
              </span>
            ))}
          </div>
          <div className="confirmation-details">
            <div>
              <small>ORDER NUMBER</small>
              <b className="mono">{order.id.slice(0, 12).toUpperCase()}</b>
            </div>
            <div>
              <small>PAID TOTAL</small>
              <b>{money(order.total)}</b>
            </div>
          </div>
        </div>
      </div>
      <div className="order-actions">
        {confirmed && (
          <>
            <Link className="button" href="/tickets">
              <Ticket size={18} /> View my tickets
            </Link>
            <a
              className="button button-outline"
              href={`/api/v1/orders/${id}/invoice`}
              target="_blank"
              rel="noopener noreferrer"
            >
              <Download size={18} /> Invoice / PDF
            </a>
          </>
        )}
        {processing && (
          <button className="button" onClick={reconcile} disabled={busy}>
            <RefreshCw size={18} className={busy ? "spin" : ""} /> Check latest
            status
          </button>
        )}
        {failed && (
          <Link className="button" href={`/events/${ev.id}/seats`}>
            Choose seats again
          </Link>
        )}
        <Link className="text-link" href="/tickets?tab=orders">
          Order history
        </Link>
      </div>
      {processing && payment?.status === "REQUIRES_ACTION" && (
        <div className="notice">
          <CreditCard size={20} />
          <p>
            Payment authorization still needs your attention.{" "}
            <Link className="text-link" href={"/checkout/" + order.holdId}>
              Return to checkout
            </Link>
          </p>
        </div>
      )}
      {confirmed && !view.refund && (
        <button
          className="text-link refund-link"
          onClick={() => setRefund(true)}
        >
          Request a refund
        </button>
      )}
      {view.refund && (
        <div className="notice">
          <Receipt size={20} />
          <div>
            <b>Refund {view.refund.status.toLowerCase()}</b>
            <p>{view.refund.reason}</p>
            <span>{money(view.refund.amount)}</span>
          </div>
        </div>
      )}
      {refund && (
        <Modal title="Request a refund" onClose={() => setRefund(false)}>
          <p>
            Refunds are reviewed by our team. Tickets must be unused and
            untransferred, and the event must be more than 24 hours away.
          </p>
          <form onSubmit={requestRefund}>
            <Field label="Reason for refund">
              <textarea
                required
                minLength={5}
                maxLength={500}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                placeholder="Tell us a little about your request"
              />
            </Field>
            <button className="button button-full" disabled={busy}>
              {busy ? "Sending…" : "Send refund request"}
            </button>
          </form>
        </Modal>
      )}
    </div>
  );
}
