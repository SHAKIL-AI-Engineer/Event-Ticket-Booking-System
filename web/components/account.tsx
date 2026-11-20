"use client";
import { useEffect, useState, Suspense } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import {
  Ticket,
  Eye,
  EyeOff,
  ShieldCheck,
  UserRound,
  Mail,
  LockKeyhole,
  Bell,
  Heart,
  Check,
  Loader2,
  CalendarDays,
  MapPin,
  Download,
  Send,
  Copy,
  Clock3,
  Receipt,
  Settings,
} from "lucide-react";
import { api, date, json, money, time, utc } from "@/lib/api";
import type { Order, Event, TicketItem, User } from "@/lib/types";
import { useApp } from "./providers";
import { Badge, Empty, ErrorState, Field, Guard, Loading, Modal } from "./ui";
export function AuthPage({ register = false }: { register?: boolean }) {
  const { refresh, config, notify } = useApp(),
    router = useRouter(),
    [name, setName] = useState(""),
    [email, setEmail] = useState(""),
    [password, setPassword] = useState(""),
    [show, setShow] = useState(false),
    [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  async function submit(e: React.FormEvent) {
    e.preventDefault();
    await login(email, password);
  }
  async function login(email: string, password: string) {
    setBusy(true);
    setError("");
    try {
      const result = await api<{ user: User }>(
        register ? "/auth/register" : "/auth/login",
        {
          method: "POST",
          body: json(
            register ? { name, email, password } : { email, password },
          ),
        },
      );
      await refresh();
      let redirect = "/tickets";
      const requested = new URLSearchParams(location.search).get("next");
      if (
        requested?.startsWith("/") &&
        !requested.includes("\\") &&
        new URL(requested, location.origin).origin === location.origin &&
        !requested.startsWith("//") &&
        !requested.startsWith("/login") &&
        !requested.startsWith("/register")
      )
        redirect = requested;
      else if (result.user.role === "Organizer") redirect = "/organizer";
      else if (result.user.role === "Admin") redirect = "/admin";
      else if (result.user.role === "Staff") redirect = "/check-in";
      router.push(redirect);
      notify(
        register
          ? "You’re in. Let’s make some memories."
          : "Welcome back. Your next moment is waiting.",
      );
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="container auth-layout">
      <div className="auth-story">
        <img
          src="/images/festival.jpg"
          alt="An unforgettable crowd at a live festival"
        />
        <div />
        <span className="eyebrow">LIFE HAPPENS LIVE.</span>
        <h1>
          Be there
          <br />
          for the <em>good stuff.</em>
        </h1>
        <p>
          The music. The laughter. The memories.
          <br />
          Your people are out there.
        </p>
        <span className="auth-story-bottom">
          <Ticket size={22} /> One ticket. A whole new story.
        </span>
      </div>
      <section className="auth-form-wrap">
        <span className="eyebrow">
          {register ? "YOUR NEXT CHAPTER STARTS HERE" : "GOOD TO HAVE YOU BACK"}
        </span>
        <h2>{register ? "Make room for more." : "Your next moment awaits."}</h2>
        <p>
          {register
            ? "Create your account. Find your kind of live."
            : "Sign in and pick up where you left off."}
        </p>
        <form onSubmit={submit}>
          {register && (
            <Field label="Your name">
              <input
                required
                minLength={2}
                maxLength={100}
                autoComplete="name"
                placeholder="Full name"
                value={name}
                onChange={(e) => setName(e.target.value)}
              />
            </Field>
          )}
          <Field label="Email address">
            <input
              type="email"
              required
              maxLength={254}
              autoComplete="email"
              placeholder="you@example.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </Field>
          <Field
            label="Password"
            hint={
              register ? "At least 10 characters. Make it your own." : undefined
            }
          >
            <div className="password-input">
              <input
                type={show ? "text" : "password"}
                required
                minLength={register ? 10 : 1}
                maxLength={128}
                autoComplete={register ? "new-password" : "current-password"}
                placeholder={
                  register ? "Create a strong password" : "Your password"
                }
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
              <button
                type="button"
                className="icon-button"
                onClick={() => setShow(!show)}
                aria-label={show ? "Hide password" : "Show password"}
              >
                {show ? <EyeOff size={18} /> : <Eye size={18} />}
              </button>
            </div>
          </Field>
          {error && (
            <p className="form-error" role="alert">
              {error}
            </p>
          )}
          <button className="button button-full" disabled={busy}>
            {busy ? (
              <Loader2 className="spin" size={18} />
            ) : (
              <>
                {register ? "Create account" : "Sign in"}{" "}
                <UserRound size={17} />
              </>
            )}
          </button>
        </form>
        <p className="auth-switch">
          {register ? "Already part of the story?" : "New around here?"}{" "}
          <Link href={register ? "/login" : "/register"}>
            {register ? "Sign in" : "Create an account"}
          </Link>
        </p>
        {!register && config?.demoAvailable && (
          <div className="demo-login">
            <span>EXPLORE THE DEMO</span>
            <p>Try a role. No signup needed.</p>
            <div>
              {[
                { label: "Attendee", email: "maya@example.com" },
                { label: "Organizer", email: "host@eventhub.test" },
                { label: "Admin", email: "admin@eventhub.test" },
                { label: "Venue staff", email: "gate@eventhub.test" },
              ].map((d) => (
                <button
                  className="button button-outline button-small"
                  disabled={busy}
                  key={d.email}
                  onClick={() => login(d.email, "EventHub!2026")}
                >
                  {d.label}
                </button>
              ))}
            </div>
          </div>
        )}
        <span className="auth-secure">
          <ShieldCheck size={16} /> Your details stay yours. Always.
        </span>
      </section>
    </div>
  );
}
export function TicketsPage() {
  return (
    <Guard>
      <Suspense fallback={<Loading />}>
        <TicketsContent />
      </Suspense>
    </Guard>
  );
}
function TicketsContent() {
  const params = useSearchParams(),
    router = useRouter(),
    [tab, setTab] = useState(
      params.get("tab") === "orders" ? "orders" : "upcoming",
    ),
    [tickets, setTickets] = useState<TicketItem[]>([]),
    [orders, setOrders] = useState<{ order: Order; ev: Event }[]>([]),
    [loading, setLoading] = useState(true),
    [error, setError] = useState(""),
    [open, setOpen] = useState<TicketItem | null>(null);
  useEffect(() => {
    Promise.all([
      api<TicketItem[]>("/tickets/me").then(setTickets),
      api<typeof orders>("/orders").then(setOrders),
    ])
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  }, []);
  const visible = tickets.filter((t) =>
    tab === "upcoming"
      ? t.ticket.isActive && new Date(utc(t.ev.endsAt)) >= new Date()
      : tab === "past"
        ? new Date(utc(t.ev.endsAt)) < new Date()
        : !t.ticket.isActive,
  );
  return (
    <div className="container page">
      <div className="page-title">
        <span className="eyebrow">GOOD TIMES, ALL IN ONE PLACE</span>
        <h1>
          Your tickets to <em>more.</em>
        </h1>
        <p>All your plans. All the details. Ready when you are.</p>
      </div>
      <div className="page-tabs">
        {[
          ["upcoming", "Upcoming"],
          ["past", "Past events"],
          ["cancelled", "Refunded"],
          ["orders", "Order history"],
        ].map(([value, label]) => (
          <button
            key={value}
            className={tab === value ? "active" : ""}
            onClick={() => {
              setTab(value);
              router.replace(
                "/tickets" + (value === "orders" ? "?tab=orders" : ""),
                { scroll: false },
              );
            }}
          >
            {label}
            {value === "upcoming" && (
              <span>
                {
                  tickets.filter(
                    (t) =>
                      t.ticket.isActive &&
                      new Date(utc(t.ev.endsAt)) >= new Date(),
                  ).length
                }
              </span>
            )}
          </button>
        ))}
      </div>
      {loading ? (
        <Loading text="Gathering your good plans…" />
      ) : error ? (
        <ErrorState message={error} />
      ) : tab === "orders" ? (
        orders.length ? (
          <div className="panel table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Event</th>
                  <th>Order</th>
                  <th>Date booked</th>
                  <th>Amount</th>
                  <th>Status</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {orders.map(({ order: o, ev }) => (
                  <tr key={o.id}>
                    <td>
                      <strong>{ev.title}</strong>
                    </td>
                    <td className="mono">{o.id.slice(0, 8).toUpperCase()}</td>
                    <td>{date(o.createdAt)}</td>
                    <td>{money(o.total)}</td>
                    <td>
                      <Badge status={o.status} />
                    </td>
                    <td>
                      <Link className="text-link" href={"/orders/" + o.id}>
                        View order
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <Empty
            title="The first chapter is yours to write"
            text="Your bookings will appear here as soon as you reserve your first experience."
            action="Find an event"
            href="/events"
          />
        )
      ) : visible.length ? (
        <div className="my-tickets-grid">
          {visible.map((item) => (
            <article key={item.ticket.id} className="my-ticket panel">
              <div className="my-ticket-cover">
                <img src={item.ev.image} alt={item.ev.title} />
                <Badge status={item.ticket.status} />
              </div>
              <div className="my-ticket-content">
                <span className="eyebrow">{item.ev.category}</span>
                <h2>{item.ev.title}</h2>
                <p>
                  <CalendarDays size={16} />
                  {date(item.ev.startsAt)} · {time(item.ev.startsAt)} BST
                </p>
                <p>
                  <MapPin size={16} />
                  {item.venue.name}
                </p>
                <div className="ticket-perforation" />
                <div className="my-ticket-bottom">
                  <div>
                    <small>YOUR SEAT</small>
                    <strong>
                      {item.seat.section} · {item.seat.row}
                      {item.seat.number}
                    </strong>
                  </div>
                  <button
                    className="button button-small"
                    onClick={() => setOpen(item)}
                  >
                    View ticket <Ticket size={16} />
                  </button>
                </div>
              </div>
            </article>
          ))}
        </div>
      ) : (
        <Empty
          title={
            tab === "upcoming"
              ? "Your next great story is waiting."
              : "No tickets here just yet."
          }
          text="Pick an experience, find your favorite seat, and give yourself something to look forward to."
          action="Explore events"
          href="/events"
        />
      )}
      {open && (
        <TicketModal
          item={open}
          onClose={() => setOpen(null)}
          onTransfer={() => {
            setOpen(null);
            void api<TicketItem[]>("/tickets/me").then(setTickets);
          }}
        />
      )}
    </div>
  );
}
export function TicketModal({
  item,
  onClose,
  onTransfer,
}: {
  item: TicketItem;
  onClose: () => void;
  onTransfer: () => void;
}) {
  const { notify } = useApp(),
    [transfer, setTransfer] = useState(false),
    [email, setEmail] = useState(""),
    [busy, setBusy] = useState(false),
    { ticket, ev, seat, venue } = item;
  async function send(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      await api("/tickets/" + ticket.id + "/transfer", {
        method: "POST",
        body: json({ email }),
      });
      notify(
        "Your ticket has a new home. The previous QR code is now invalid.",
      );
      onTransfer();
    } catch (e) {
      notify((e as Error).message, true);
    } finally {
      setBusy(false);
    }
  }
  return (
    <Modal title="Your ticket to a good time" onClose={onClose}>
      <div className="ticket-preview">
        <img className="ticket-preview-cover" src={ev.image} alt={ev.title} />
        <span className="eyebrow">EVENTHUB · ADMIT ONE</span>
        <h2>{ev.title}</h2>
        <p>
          {date(ev.startsAt)} · {time(ev.startsAt)} BST
          <br />
          {venue.name}, {venue.city}
        </p>
        <div className="ticket-preview-seat">
          <span>
            <small>SECTION</small>
            <b>{seat.section}</b>
          </span>
          <span>
            <small>ROW</small>
            <b>{seat.row}</b>
          </span>
          <span>
            <small>SEAT</small>
            <b>{seat.number}</b>
          </span>
        </div>
        <div className="ticket-perforation" />
        {ticket.isActive ? (
          <>
            <img
              className="ticket-qr"
              src={"/api/v1/tickets/" + ticket.id + "/qr"}
              alt="Your unique ticket QR code. Present this at venue entry."
            />
            <span className="ticket-code mono">
              {ticket.id.slice(0, 12).toUpperCase()}
            </span>
            <p className="ticket-security">
              Keep your QR code private. Each ticket admits one person.
            </p>
          </>
        ) : (
          <Badge status={ticket.status} />
        )}
      </div>
      <div className="ticket-modal-actions">
        {ticket.isActive && (
          <>
            <a
              href={"/api/v1/tickets/" + ticket.id + "/qr"}
              download={`eventhub-${seat.row}${seat.number}.svg`}
              className="button button-outline button-small"
            >
              <Download size={16} /> Save QR
            </a>
            <a
              href={"/api/v1/tickets/" + ticket.id + "/calendar"}
              className="button button-outline button-small"
            >
              <CalendarDays size={16} /> Add to calendar
            </a>
          </>
        )}
        <Link href={"/orders/" + ticket.orderId} className="text-link">
          View order
        </Link>
      </div>
      {ticket.isActive &&
        ticket.status === "VALID" &&
        ticket.transferCount === 0 && (
          <>
            <button
              className="text-link"
              onClick={() => setTransfer(!transfer)}
            >
              <Send size={16} /> Transfer to a friend
            </button>
            {transfer && (
              <form className="transfer-form" onSubmit={send}>
                <p>
                  Transfers are final and allowed once. Your current QR code
                  will stop working. Your friend needs an EventHub account.
                </p>
                <Field label="Friend’s account email">
                  <input
                    required
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    placeholder="friend@example.com"
                  />
                </Field>
                <button className="button button-full" disabled={busy}>
                  {busy ? "Transferring…" : "Confirm ticket transfer"}
                </button>
              </form>
            )}
          </>
        )}
    </Modal>
  );
}
type Note = {
  id: number;
  title: string;
  body: string;
  read: boolean;
  createdAt: string;
};
export function AccountPage() {
  return (
    <Guard>
      <Suspense fallback={<Loading />}>
        <AccountContent />
      </Suspense>
    </Guard>
  );
}
function AccountContent() {
  const { user, refresh, notify } = useApp(),
    params = useSearchParams(),
    [tab, setTab] = useState(params.get("tab") || "profile"),
    [name, setName] = useState(user?.name || ""),
    [current, setCurrent] = useState(""),
    [password, setPassword] = useState(""),
    [notes, setNotes] = useState<Note[]>([]),
    [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  useEffect(() => {
    api<Note[]>("/notifications")
      .then(setNotes)
      .catch((e) => setError(e.message));
  }, []);
  async function save(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      await api("/account", { method: "PUT", body: json({ name }) });
      await refresh();
      notify("Your profile is up to date.");
    } catch (e) {
      notify((e as Error).message, true);
    } finally {
      setBusy(false);
    }
  }
  async function changePassword(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      await api("/account/password", {
        method: "PUT",
        body: json({ currentPassword: current, newPassword: password }),
      });
      setCurrent("");
      setPassword("");
      notify("Password updated. Other sessions have been signed out.");
    } catch (e) {
      notify((e as Error).message, true);
    } finally {
      setBusy(false);
    }
  }
  async function markRead() {
    try {
      await api("/notifications/read", { method: "PUT" });
      setNotes((n) => n.map((x) => ({ ...x, read: true })));
    } catch (e) {
      notify((e as Error).message, true);
    }
  }
  return (
    <div className="container page">
      <div className="page-title">
        <span className="eyebrow">MAKE YOURSELF AT HOME</span>
        <h1>Your account.</h1>
        <p>A little about you, for better experiences.</p>
      </div>
      <div className="account-layout">
        <nav className="account-nav">
          {[
            { id: "profile", label: "My profile", icon: UserRound },
            { id: "security", label: "Password & security", icon: LockKeyhole },
            { id: "notifications", label: "Notifications", icon: Bell },
          ].map((x) => (
            <button
              className={tab === x.id ? "active" : ""}
              key={x.id}
              onClick={() => setTab(x.id)}
            >
              <x.icon size={19} />
              {x.label}
            </button>
          ))}
          <Link href="/saved">
            <Heart size={19} />
            Saved events
          </Link>
          <span className="account-role">
            Account role <Badge status={user?.role || "Customer"} />
          </span>
        </nav>
        <section className="panel account-panel">
          {tab === "profile" ? (
            <>
              <h2>The person behind the plans</h2>
              <p>Keep your details up to date.</p>
              <form onSubmit={save}>
                <Field label="Full name">
                  <input
                    required
                    minLength={2}
                    maxLength={100}
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                  />
                </Field>
                <Field
                  label="Email address"
                  hint="Your email identifies your account and ticket transfers."
                >
                  <input value={user?.email} readOnly />
                </Field>
                <button className="button" disabled={busy}>
                  {busy ? "Saving…" : "Save changes"}
                </button>
              </form>
            </>
          ) : tab === "security" ? (
            <>
              <h2>A little extra peace of mind</h2>
              <p>Choose a unique password with at least 10 characters.</p>
              <form onSubmit={changePassword}>
                <Field label="Current password">
                  <input
                    type="password"
                    autoComplete="current-password"
                    required
                    value={current}
                    onChange={(e) => setCurrent(e.target.value)}
                  />
                </Field>
                <Field label="New password">
                  <input
                    type="password"
                    autoComplete="new-password"
                    required
                    minLength={10}
                    maxLength={128}
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                  />
                </Field>
                <button className="button" disabled={busy}>
                  {busy ? "Updating…" : "Update password"}
                </button>
              </form>
            </>
          ) : (
            <>
              <div className="section-heading">
                <h2>In the loop</h2>
                <button className="text-link" onClick={markRead}>
                  Mark all as read
                </button>
              </div>
              {error ? (
                <ErrorState message={error} />
              ) : notes.length ? (
                notes.map((n) => (
                  <article
                    className={`notification-item ${n.read ? "" : "unread"}`}
                    key={n.id}
                  >
                    <span>
                      <Bell size={19} />
                    </span>
                    <div>
                      <strong>{n.title}</strong>
                      <p>{n.body}</p>
                      <small>{date(n.createdAt)}</small>
                    </div>
                  </article>
                ))
              ) : (
                <Empty
                  title="All caught up"
                  text="Your booking updates and ticket news will appear here."
                />
              )}
            </>
          )}
        </section>
      </div>
    </div>
  );
}
