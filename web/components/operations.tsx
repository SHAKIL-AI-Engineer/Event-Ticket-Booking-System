"use client";
import { useCallback, useEffect, useRef, useState, Suspense } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import {
  Activity,
  AlertCircle,
  Camera,
  Check,
  CheckCircle2,
  ClipboardList,
  Loader2,
  RefreshCw,
  ScanLine,
  ShieldCheck,
  Ticket,
  Users,
  Wallet,
  X,
} from "lucide-react";
import { api, date, json, money, time } from "@/lib/api";
import type { User, Refund, Event } from "@/lib/types";
import { useApp } from "./providers";
import { Badge, Empty, ErrorState, Field, Guard, Loading, Modal } from "./ui";
import { DashboardShell } from "./management";
type Exception = {
  id: string;
  orderId: string;
  provider: string;
  status: string;
  updatedAt: string;
  error?: string;
};
type Audit = {
  id: number;
  actorId: string;
  action: string;
  target: string;
  detail: string;
  createdAt: string;
};
type AdminData = {
  activeHolds: number;
  confirmedOrders: number;
  pendingPayments: number;
  pendingRefunds: number;
  outboxPending: number;
  users: number;
  exceptions: Exception[];
  refunds: Refund[];
  audit: Audit[];
};
export function AdminPage() {
  return (
    <Guard roles={["Admin"]}>
      <Suspense fallback={<Loading />}>
        <DashboardShell admin>
          <AdminContent />
        </DashboardShell>
      </Suspense>
    </Guard>
  );
}
function AdminContent() {
  const params = useSearchParams(),
    router = useRouter(),
    tab = params.get("tab") || "overview",
    { notify } = useApp(),
    [data, setData] = useState<AdminData>(),
    [users, setUsers] = useState<User[]>([]),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(""),
    [review, setReview] = useState<{ id: string; approve: boolean } | null>(
      null,
    ),
    [query, setQuery] = useState("");
  const load = useCallback(
    () =>
      Promise.all([
        api<AdminData>("/admin/overview").then(setData),
        api<User[]>("/admin/users").then(setUsers),
      ]),
    [],
  );
  useEffect(() => {
    void load().catch((e) => setError(e.message));
  }, [load]);
  async function run(
    id: string,
    action: () => Promise<unknown>,
    message: string,
  ) {
    setBusy(id);
    try {
      await action();
      await load();
      notify(message);
    } catch (e) {
      notify((e as Error).message, true);
    } finally {
      setBusy("");
      setReview(null);
    }
  }
  if (error)
    return (
      <ErrorState
        message={error}
        retry={() => {
          setError("");
          void load().catch((e) => setError(e.message));
        }}
      />
    );
  if (!data) return <Loading text="Checking platform operations…" />;
  return (
    <>
      <div className="dashboard-title">
        <div>
          <span className="eyebrow">THE CONFIDENCE BEHIND EVERY BOOKING</span>
          <h1>Platform operations.</h1>
          <p>Real booking activity. Clear next actions.</p>
        </div>
        <button
          className="button button-outline"
          disabled={busy === "refresh"}
          onClick={() => run("refresh", load, "Operations are up to date.")}
        >
          <RefreshCw size={15} className={busy === "refresh" ? "spin" : ""} />
          Refresh
        </button>
      </div>
      <div className="stats-grid">
        {[
          {
            label: "Active holds",
            value: data.activeHolds,
            detail: "Unexpired seat reservations",
            icon: Ticket,
          },
          {
            label: "Confirmed orders",
            value: data.confirmedOrders,
            detail: "Completed bookings",
            icon: CheckCircle2,
          },
          {
            label: "Payment recovery",
            value: data.pendingPayments,
            detail: "Awaiting a definitive outcome",
            icon: Wallet,
          },
          {
            label: "Pending refunds",
            value: data.pendingRefunds,
            detail: "Requests needing attention",
            icon: Activity,
          },
        ].map((s) => (
          <div className="stat-card" key={s.label}>
            <span className="stat-label">
              {s.label}
              <s.icon size={18} />
            </span>
            <strong>{s.value}</strong>
            <small>{s.detail}</small>
          </div>
        ))}
      </div>
      <div className="page-tabs admin-tabs">
        {[
          ["overview", "Overview"],
          ["payments", "Payments"],
          ["refunds", "Refund reviews"],
          ["users", "People & access"],
          ["audit", "Audit trail"],
        ].map(([value, label]) => (
          <button
            key={value}
            className={tab === value ? "active" : ""}
            onClick={() =>
              router.replace("/admin?tab=" + value, { scroll: false })
            }
          >
            {label}
          </button>
        ))}
      </div>
      {tab === "overview" ? (
        <>
          <div className="notice">
            <ShieldCheck size={22} />
            <div>
              <b>Your authoritative database is the source of truth.</b>
              <p>
                {data.users} registered accounts · {data.outboxPending}{" "}
                unpublished outbox events. Payment outcomes marked unknown keep
                allocated seats until reconciliation resolves them.
              </p>
            </div>
          </div>
          <div className="dashboard-section-header">
            <h2>Payments needing attention</h2>
            <button
              className="text-link"
              onClick={() => router.push("/admin?tab=payments")}
            >
              View payment recovery
            </button>
          </div>
          <PaymentTable
            items={data.exceptions.slice(0, 5)}
            busy={busy}
            reconcile={(id) =>
              run(
                id,
                () =>
                  api("/admin/payments/" + id + "/reconcile", {
                    method: "POST",
                  }),
                "Payment reconciliation checked.",
              )
            }
          />
          <div className="dashboard-section-header">
            <h2>Recent activity</h2>
          </div>
          <AuditTable items={data.audit.slice(0, 8)} />
        </>
      ) : tab === "payments" ? (
        <>
          <p className="inline-info">
            <AlertCircle size={15} />
            Reconciliation checks the existing payment attempt. It never starts
            an independent charge.
          </p>
          <PaymentTable
            items={data.exceptions}
            busy={busy}
            reconcile={(id) =>
              run(
                id,
                () =>
                  api("/admin/payments/" + id + "/reconcile", {
                    method: "POST",
                  }),
                "Payment reconciliation checked.",
              )
            }
          />
        </>
      ) : tab === "refunds" ? (
        data.refunds.length ? (
          <div className="panel table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Order</th>
                  <th>Amount</th>
                  <th>Reason</th>
                  <th>Status</th>
                  <th>Review</th>
                </tr>
              </thead>
              <tbody>
                {data.refunds.map((r) => (
                  <tr key={r.id}>
                    <td className="mono">
                      {r.orderId.slice(0, 8).toUpperCase()}
                    </td>
                    <td>
                      <strong>{money(r.amount)}</strong>
                    </td>
                    <td title={r.reason}>
                      {r.reason.length > 45
                        ? r.reason.slice(0, 45) + "…"
                        : r.reason}
                    </td>
                    <td>
                      <Badge status={r.status} />
                    </td>
                    <td>
                      {["REQUESTED", "PROCESSING"].includes(r.status) && (
                        <div className="table-actions">
                          <button
                            disabled={busy === r.id}
                            onClick={() =>
                              setReview({ id: r.id, approve: true })
                            }
                          >
                            {r.status === "PROCESSING"
                              ? "Reconcile"
                              : "Approve"}
                          </button>
                          {r.status === "REQUESTED" && (
                            <button
                              className="text-danger"
                              disabled={busy === r.id}
                              onClick={() =>
                                setReview({ id: r.id, approve: false })
                              }
                            >
                              Reject
                            </button>
                          )}
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <Empty
            title="A little peace and quiet"
            text="Refund requests will appear here when a customer needs a hand."
          />
        )
      ) : tab === "users" ? (
        <>
          <Field label="Find a person">
            <input
              placeholder="Search by name or email"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
          </Field>
          <div className="panel table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Person</th>
                  <th>Email</th>
                  <th>Account role</th>
                  <th>Update role</th>
                </tr>
              </thead>
              <tbody>
                {users
                  .filter((u) =>
                    (u.name + " " + u.email)
                      .toLowerCase()
                      .includes(query.toLowerCase()),
                  )
                  .map((u) => (
                    <tr key={u.id}>
                      <td>
                        <strong>{u.name}</strong>
                      </td>
                      <td>{u.email}</td>
                      <td>
                        <Badge status={u.role} />
                      </td>
                      <td>
                        <select
                          aria-label={`Change ${u.name}'s role`}
                          value={u.role}
                          disabled={busy === u.id}
                          onChange={(e) => {
                            const role = e.target.value;
                            void run(
                              u.id,
                              () =>
                                api("/admin/users/" + u.id + "/role", {
                                  method: "PUT",
                                  body: json({ role }),
                                }),
                              "Account role updated.",
                            );
                          }}
                        >
                          {["Customer", "Organizer", "Staff", "Admin"].map(
                            (r) => (
                              <option key={r}>{r}</option>
                            ),
                          )}
                        </select>
                      </td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        </>
      ) : (
        <AuditTable items={data.audit} />
      )}{" "}
      {review && (
        <Modal
          title={
            review.approve ? "Approve this refund?" : "Reject this refund?"
          }
          onClose={() => setReview(null)}
        >
          <p>
            {review.approve
              ? "The payment provider will refund the full order amount. Tickets will be revoked and their seats released only after the provider confirms the refund."
              : "The order will return to confirmed status. Its tickets will remain valid."}
          </p>
          <button
            className="button button-full"
            disabled={busy === review.id}
            onClick={() =>
              run(
                review.id,
                () =>
                  api("/admin/refunds/" + review.id + "/review", {
                    method: "POST",
                    body: json({ approve: review.approve }),
                  }),
                review.approve
                  ? "Refund confirmed. Tickets have been revoked."
                  : "Refund request rejected.",
              )
            }
          >
            {busy === review.id
              ? "Checking provider…"
              : review.approve
                ? "Confirm refund approval"
                : "Confirm rejection"}
          </button>
        </Modal>
      )}
    </>
  );
}
function PaymentTable({
  items,
  busy,
  reconcile,
}: {
  items: Exception[];
  busy: string;
  reconcile: (id: string) => void;
}) {
  return items.length ? (
    <div className="panel table-wrap">
      <table>
        <thead>
          <tr>
            <th>Order</th>
            <th>Provider</th>
            <th>State</th>
            <th>Last update</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {items.map((p) => (
            <tr key={p.id}>
              <td className="mono">{p.orderId.slice(0, 8).toUpperCase()}</td>
              <td>{p.provider}</td>
              <td>
                <Badge status={p.status} />
              </td>
              <td>
                {date(p.updatedAt, { year: undefined })} · {time(p.updatedAt)}
              </td>
              <td>
                <button
                  className="text-link"
                  disabled={busy === p.orderId}
                  onClick={() => reconcile(p.orderId)}
                >
                  {busy === p.orderId ? (
                    <Loader2 className="spin" size={15} />
                  ) : (
                    <RefreshCw size={14} />
                  )}{" "}
                  Reconcile
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  ) : (
    <div className="panel empty-state" style={{ minHeight: 210 }}>
      <CheckCircle2 size={32} />
      <h2>All clear.</h2>
      <p>There are no payments awaiting recovery.</p>
    </div>
  );
}
function AuditTable({ items }: { items: Audit[] }) {
  return items.length ? (
    <div className="panel table-wrap">
      <table>
        <thead>
          <tr>
            <th>Action</th>
            <th>Actor</th>
            <th>Reference</th>
            <th>Details</th>
            <th>When</th>
          </tr>
        </thead>
        <tbody>
          {items.map((x) => (
            <tr key={x.id}>
              <td className="audit-action">{x.action}</td>
              <td className="mono">{x.actorId.slice(0, 12)}</td>
              <td className="mono">{x.target.slice(0, 10)}</td>
              <td title={x.detail}>{x.detail.slice(0, 60) || "—"}</td>
              <td>
                {date(x.createdAt, { year: undefined })} · {time(x.createdAt)}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  ) : (
    <Empty
      title="A fresh start"
      text="Booking, payment, and account actions will appear in this audit trail."
    />
  );
}
// Camera access is optional. A pasted signed QR token works in every supported browser.
type BarcodeDetectorInstance = {
  detect: (source: HTMLVideoElement) => Promise<{ rawValue: string }[]>;
};
declare global {
  interface Window {
    BarcodeDetector?: new (options: {
      formats: string[];
    }) => BarcodeDetectorInstance;
  }
}
export function CheckInPage() {
  return (
    <Guard roles={["Staff", "Organizer", "Admin"]}>
      <DashboardShell>
        <CheckInContent />
      </DashboardShell>
    </Guard>
  );
}
function CheckInContent() {
  const { notify } = useApp(),
    [events, setEvents] = useState<Event[]>([]),
    [eventId, setEventId] = useState(0),
    [gate, setGate] = useState("Main entrance"),
    [token, setToken] = useState(""),
    [busy, setBusy] = useState(false),
    [error, setError] = useState(""),
    [result, setResult] = useState<{
      attendee: string;
      seat: string;
      eventTitle: string;
      checkedInAt: string;
    }>(),
    [camera, setCamera] = useState(false),
    [cameraError, setCameraError] = useState(""),
    video = useRef<HTMLVideoElement>(null);
  useEffect(() => {
    api<Event[]>("/staff/events")
      .then((items) => {
        setEvents(items);
        setEventId(items[0]?.id || 0);
      })
      .catch((e) => setError(e.message));
  }, []);
  useEffect(() => {
    if (!camera) return;
    let stop = false,
      stream: MediaStream | undefined,
      timer: ReturnType<typeof setInterval> | undefined;
    async function start() {
      try {
        if (!window.BarcodeDetector)
          throw new Error(
            "This browser does not support QR detection. Use Chrome on Android, or paste the signed ticket code below.",
          );
        if (!navigator.mediaDevices)
          throw new Error(
            "Camera access needs HTTPS and a supported browser. You can paste the ticket code below.",
          );
        stream = await navigator.mediaDevices.getUserMedia({
          video: { facingMode: "environment" },
        });
        if (stop) {
          stream.getTracks().forEach((t) => t.stop());
          return;
        }
        if (video.current) {
          video.current.srcObject = stream;
          await video.current.play();
        }
        const detector = new window.BarcodeDetector({ formats: ["qr_code"] });
        timer = setInterval(async () => {
          if (stop || !video.current || video.current.readyState < 2) return;
          try {
            const codes = await detector.detect(video.current);
            if (codes[0]) {
              setToken(codes[0].rawValue);
              setCamera(false);
              notify(
                "Ticket code captured. Confirm the event and validate entry.",
              );
            }
          } catch {}
        }, 500);
      } catch (e) {
        setCameraError((e as Error).message);
        setCamera(false);
      }
    }
    void start();
    return () => {
      stop = true;
      clearInterval(timer);
      stream?.getTracks().forEach((t) => t.stop());
    };
  }, [camera, notify]);
  async function scan(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setResult(undefined);
    setError("");
    try {
      const r = await api<NonNullable<typeof result>>("/tickets/check-in", {
        method: "POST",
        body: json({ token: token.trim(), eventId, gate }),
      });
      setResult(r);
      setToken("");
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <div className="dashboard-title">
        <div>
          <span className="eyebrow">WELCOME THEM TO THE MOMENT</span>
          <h1>A smooth way in.</h1>
          <p>Validate tickets and get the good times started.</p>
        </div>
        <Badge status="Staff" />
      </div>
      <div className="scanner-layout">
        <section className="panel scanner-panel">
          <h2>Venue check-in</h2>
          <p className="camera-note">
            Check-in opens 2 hours before the event. Every ticket can be used
            once.
          </p>
          <form onSubmit={scan}>
            <Field label="Event">
              <select
                value={eventId}
                onChange={(e) => setEventId(Number(e.target.value))}
              >
                {events.map((e) => (
                  <option key={e.id} value={e.id}>
                    {e.title} · {date(e.startsAt, { year: undefined })}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Gate / entrance">
              <input
                required
                maxLength={60}
                value={gate}
                onChange={(e) => setGate(e.target.value)}
              />
            </Field>
            {camera ? (
              <>
                <video ref={video} className="camera-video" muted playsInline />
                <button
                  type="button"
                  className="text-link"
                  onClick={() => setCamera(false)}
                >
                  <X size={16} />
                  Close camera
                </button>
              </>
            ) : (
              <button
                type="button"
                className="scanner-placeholder button-full"
                onClick={() => {
                  setCameraError("");
                  setCamera(true);
                }}
              >
                <span>
                  <Camera size={36} style={{ margin: "0 auto 10px" }} />
                  <span style={{ display: "block", fontSize: 12 }}>
                    Open QR scanner
                  </span>
                </span>
              </button>
            )}
            {cameraError && (
              <p className="camera-note" role="status">
                {cameraError}
              </p>
            )}
            <Field
              label="Signed ticket code"
              hint="Paste the full code from the QR scanner, including its signature."
            >
              <textarea
                className="code-input"
                required
                minLength={20}
                maxLength={256}
                value={token}
                onChange={(e) => setToken(e.target.value)}
                placeholder="Ticket ID.version.signature"
              />
            </Field>
            <button className="button button-full" disabled={busy || !eventId}>
              {busy ? (
                <Loader2 size={18} className="spin" />
              ) : (
                <ScanLine size={18} />
              )}
              Validate entry
            </button>
          </form>
        </section>
        <section>
          <div className="panel scanner-panel">
            <ShieldCheck size={30} color="#83a363" />
            <h2 style={{ marginTop: 15 }}>
              A warm welcome.
              <br />A secure entry.
            </h2>
            <p className="camera-note">
              Confirm the event and entrance before each scan. A transferred
              ticket’s previous QR code is immediately invalidated. Refunds and
              duplicate scans are rejected by the server.
            </p>
            <div className="notice">
              <Ticket size={20} />
              <div>
                <b>One ticket. One entry.</b>
                <p>
                  Ticket checks are processed in a database transaction, even
                  when multiple gates scan at once.
                </p>
              </div>
            </div>
          </div>
          {error && (
            <div className="form-error" role="alert">
              <AlertCircle size={22} />
              <h3 style={{ margin: "10px 0" }}>Entry not validated</h3>
              {error}
            </div>
          )}
          {result && (
            <div className="scan-success" role="status">
              <CheckCircle2 size={36} />
              <h3>Welcome, {result.attendee}.</h3>
              <p>{result.eventTitle}</p>
              <p>{result.seat}</p>
              <small>
                Checked in at {time(result.checkedInAt)} BST · {gate}
              </small>
            </div>
          )}
        </section>
      </div>
    </>
  );
}
