"use client";
import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  CalendarDays,
  Clock3,
  MapPin,
  Heart,
  Ticket,
  ShieldCheck,
  Music2,
  Check,
  Plus,
  Minus,
  List,
  Grid2X2,
  Info,
  Loader2,
  Users,
  Wifi,
  WifiOff,
  Timer,
  Armchair,
  Share2,
} from "lucide-react";
import { api, ApiError, date, json, money, time, utc } from "@/lib/api";
import type { EventItem, Seat, Hold } from "@/lib/types";
import { useApp } from "./providers";
import { Breadcrumb, Empty, ErrorState, Guard, Loading, Steps } from "./ui";
export function EventDetail({ id }: { id: number }) {
  const [data, setData] = useState<EventItem | null>(null),
    [error, setError] = useState(""),
    { favorites, toggleFavorite, notify } = useApp();
  useEffect(() => {
    api<EventItem>("/events/" + id)
      .then(setData)
      .catch((e) => setError(e.message));
  }, [id]);
  if (error)
    return (
      <div className="container page">
        <ErrorState message={error} />
      </div>
    );
  if (!data) return <Loading />;
  const { ev, venue } = data;
  async function share() {
    try {
      if (navigator.share)
        await navigator.share({ title: ev.title, url: location.href });
      else {
        await navigator.clipboard.writeText(location.href);
        notify("Event link copied.");
      }
    } catch {}
  }
  return (
    <div className="container event-detail page">
      <Breadcrumb
        items={[
          { label: "Discover events", href: "/events" },
          {
            label: ev.category,
            href: "/events?category=" + encodeURIComponent(ev.category),
          },
          { label: ev.title },
        ]}
      />
      <div className="detail-banner">
        <img src={ev.image} alt={`${ev.title} live experience`} />
        <div className="detail-banner-shade" />
        <div className="detail-banner-content">
          <span className="label-light">
            {ev.category} <span>·</span> LIVE EXPERIENCE
          </span>
          <h1>{ev.title}</h1>
          <p>{ev.artist}</p>
        </div>
        <div className="detail-banner-actions">
          <button
            className="icon-button"
            aria-label="Share event"
            onClick={share}
          >
            <Share2 size={19} />
          </button>
          <button
            className="icon-button"
            onClick={() => toggleFavorite(id)}
            aria-label="Save event"
            aria-pressed={favorites.includes(id)}
          >
            <Heart
              size={19}
              fill={favorites.includes(id) ? "currentColor" : "none"}
            />
          </button>
        </div>
      </div>
      <div className="detail-layout">
        <div>
          <div className="detail-info-strip">
            <span>
              <CalendarDays />
              {date(ev.startsAt)}
            </span>
            <span>
              <Clock3 />
              {time(ev.startsAt)} BST
            </span>
            <span>
              <MapPin />
              {venue.city}
            </span>
          </div>
          <section className="detail-section">
            <span className="eyebrow">BE PART OF THE MOMENT</span>
            <h2>A night worth being there for.</h2>
            {ev.description.split("\n\n").map((p, i) => (
              <p key={i}>{p}</p>
            ))}
          </section>
          <section className="detail-section">
            <h2>
              {ev.category === "Music" ? "The lineup" : "Meet your hosts"}
            </h2>
            <div className="lineup-list">
              {ev.artist.split("·").map((name, i) => (
                <div key={name}>
                  <span className={`lineup-avatar lineup-${i}`}>
                    <Music2 size={27} />
                  </span>
                  <h3>{name.trim()}</h3>
                  <span>
                    {ev.category === "Music"
                      ? "Live performance"
                      : "Featured host"}
                  </span>
                </div>
              ))}
            </div>
          </section>
          <section className="detail-section venue-section">
            <h2>The place to be</h2>
            <div className="venue-map">
              <div className="map-grid" />
              <span className="map-river" />
              <span className="map-road road-one" />
              <span className="map-road road-two" />
              <div className="map-marker">
                <MapPin size={25} />
                <span>{venue.name}</span>
              </div>
              <span className="map-city">{venue.city.toUpperCase()}</span>
            </div>
            <h3>{venue.name}</h3>
            <p>{venue.address}</p>
            <a
              className="text-link"
              href={
                "https://www.google.com/maps/search/?api=1&query=" +
                encodeURIComponent(venue.name + " " + venue.address)
              }
              target="_blank"
              rel="noopener noreferrer"
            >
              Open in Google Maps <MapPin size={15} />
            </a>
          </section>
          <section className="detail-section">
            <h2>A few things to know</h2>
            <div className="policy-list">
              {ev.policies.split(". ").map((p, i) => (
                <p key={i}>
                  <Check size={18} />
                  <span>{p.replace(/\.$/, "")}.</span>
                </p>
              ))}
            </div>
          </section>
        </div>
        <aside className="booking-aside">
          <div className="panel booking-card">
            <span className="eyebrow">YOUR NEXT GREAT STORY</span>
            <div className="booking-price">
              <small>Tickets from</small>
              <strong>{money(ev.minPrice)}</strong>
              <span>per person + 5% service fee</span>
            </div>
            <div className="booking-meta">
              <CalendarDays size={20} />
              <div>
                <b>{date(ev.startsAt, { weekday: "long" })}</b>
                <span>
                  {time(ev.startsAt)} – {time(ev.endsAt)} BST
                </span>
              </div>
            </div>
            <div className="booking-meta">
              <MapPin size={20} />
              <div>
                <b>{venue.name}</b>
                <span>{venue.city}, Bangladesh</span>
              </div>
            </div>
            <Link
              href={`/events/${id}/${ev.queueEnabled ? "waiting-room" : "seats"}`}
              className="button button-full"
            >
              Choose your seats <Ticket size={18} />
            </Link>
            <span className="booking-secure">
              <ShieldCheck size={15} /> Secure booking. Instant tickets.
            </span>
            {data.available !== undefined && (
              <div className="availability-note">
                <Armchair size={16} />
                {data.available} of {data.capacity} seats available
              </div>
            )}
          </div>
          <div className="help-note">
            <Info size={18} />
            <p>
              Need a hand with your booking?
              <br />
              <Link href="/help">We’re here to help.</Link>
            </p>
          </div>
        </aside>
      </div>
    </div>
  );
}
export function SeatSelection({ id }: { id: number }) {
  return (
    <Guard>
      <SeatSelectionContent id={id} />
    </Guard>
  );
}
function SeatSelectionContent({ id }: { id: number }) {
  const router = useRouter(),
    { notify, config } = useApp(),
    [data, setData] = useState<EventItem | null>(null),
    [seats, setSeats] = useState<Seat[]>([]),
    [selected, setSelected] = useState<number[]>([]),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false),
    [section, setSection] = useState("All"),
    [list, setList] = useState(false),
    [zoom, setZoom] = useState(1),
    [live, setLive] = useState("Connecting"),
    mapRef = useRef<HTMLDivElement>(null),
    requestKey = useRef(crypto.randomUUID()),
    requestPayload = useRef("");
  const snapshot = useCallback(async () => {
    const r = await api<{ seats: Seat[] }>(`/events/${id}/seats`);
    setSeats((old) => {
      const current = new Map(old.map((s) => [s.id, s]));
      return r.seats.map((s) => {
        const newer = current.get(s.id);
        return newer && newer.version > s.version ? newer : s;
      });
    });
  }, [id]);
  useEffect(() => {
    Promise.all([
      api<EventItem>("/events/" + id).then(setData),
      snapshot(),
    ]).catch((e) => setError(e.message));
  }, [id, snapshot]);
  useEffect(() => {
    if (config?.realtimeMode === "snapshot") {
      setLive("Updates every 15 seconds");
      return;
    }
    if (!config) return;
    let stop = false;
    let conn: import("@microsoft/signalr").HubConnection | undefined;
    import("@microsoft/signalr").then(async (signalr) => {
      if (stop) return;
      conn = new signalr.HubConnectionBuilder()
        .withUrl("/hubs/availability")
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .configureLogging(signalr.LogLevel.Error)
        .build();
      conn.on("SeatsChanged", (r: { eventId: number; seats: Seat[] }) => {
        if (r.eventId !== id) return;
        setSeats((old) =>
          old.map((s) => {
            const changed = r.seats.find((x) => x.id === s.id);
            return changed && changed.version > s.version ? changed : s;
          }),
        );
      });
      conn.onreconnecting(() => setLive("Reconnecting"));
      conn.onreconnected(async () => {
        await conn?.invoke("Subscribe", id);
        await snapshot();
        setLive("Live");
      });
      conn.onclose(() => setLive("Offline"));
      try {
        await conn.start();
        if (stop) {
          await conn.stop();
          return;
        }
        await conn.invoke("Subscribe", id);
        await snapshot();
        setLive("Live");
      } catch {
        setLive("Offline");
      }
    });
    return () => {
      stop = true;
      void conn?.stop();
    };
  }, [id, snapshot, config]);
  useEffect(() => {
    const timer = setInterval(() => {
      if (document.visibilityState === "visible")
        void snapshot().catch(() => {});
    }, 15000);
    return () => clearInterval(timer);
  }, [snapshot]);
  function choose(s: Seat) {
    if (s.status !== "AVAILABLE") return;
    setSelected((old) =>
      old.includes(s.id)
        ? old.filter((x) => x !== s.id)
        : old.length < 8
          ? [...old, s.id]
          : old,
    );
  }
  const picked = seats.filter((s) => selected.includes(s.id)),
    subtotal = picked.reduce((n, s) => n + s.price, 0),
    fee = Math.round(subtotal * 0.05),
    sections = [...new Set(seats.map((s) => s.section))],
    rows = [
      ...new Set(
        seats
          .filter((s) => section === "All" || s.section === section)
          .map((s) => s.row),
      ),
    ];
  useEffect(() => {
    setSelected((old) =>
      old.filter((id) =>
        seats.some((s) => s.id === id && s.status === "AVAILABLE"),
      ),
    );
  }, [seats]);
  async function reserve() {
    if (!picked.length) return;
    setBusy(true);
    try {
      const body = {
        eventId: id,
        seatIds: picked.map((s) => s.id).sort((a, b) => a - b),
        admissionToken: sessionStorage.getItem("eventhub-admission-" + id),
      };
      const serialized = json(body);
      if (requestPayload.current && requestPayload.current !== serialized)
        requestKey.current = crypto.randomUUID();
      requestPayload.current = serialized;
      const hold = await api<Hold>("/holds", {
        method: "POST",
        headers: { "Idempotency-Key": requestKey.current },
        body: serialized,
      });
      router.push("/checkout/" + hold.id);
    } catch (e) {
      const err = e as ApiError;
      notify(err.message, true);
      if (err.code === "ADMISSION_REQUIRED")
        router.push(`/events/${id}/waiting-room`);
      try {
        await snapshot();
      } catch {
        /* Preserve the original booking error. */
      }
    } finally {
      setBusy(false);
    }
  }
  if (error) return <ErrorState message={error} />;
  if (!data) return <Loading text="Getting the best seats in the house…" />;
  return (
    <div className="container page seat-page">
      <Breadcrumb
        items={[
          { label: "Discover events", href: "/events" },
          { label: data.ev.title, href: `/events/${id}` },
          { label: "Choose seats" },
        ]}
      />
      <Steps current={0} />
      <div className="seat-page-heading">
        <div>
          <span className="eyebrow">FIND YOUR SPOT</span>
          <h1>There’s a seat for you.</h1>
          <p>
            {data.ev.title} <span>·</span> {date(data.ev.startsAt)}{" "}
            <span>·</span> {data.venue.name}
          </p>
        </div>
        <div className={`live-status ${live === "Live" ? "connected" : ""}`}>
          {live === "Live" ? <Wifi size={16} /> : <WifiOff size={16} />}{" "}
          {live === "Live" ? "Live availability" : live}
          <button
            className="text-link"
            onClick={() => snapshot().catch((e) => notify(e.message, true))}
          >
            Refresh
          </button>
        </div>
      </div>
      <div className="seats-layout">
        <section className="panel seat-map-panel">
          <div className="seat-toolbar">
            <div className="section-prices">
              <button
                className={section === "All" ? "selected" : ""}
                onClick={() => setSection("All")}
              >
                All sections
              </button>
              {sections.map((s) => (
                <button
                  key={s}
                  className={section === s ? "selected" : ""}
                  onClick={() => setSection(s)}
                >
                  <span className={"section-dot " + s.toLowerCase()} />
                  {s}
                  <b>
                    {money(
                      Math.min(
                        ...seats
                          .filter((x) => x.section === s)
                          .map((x) => x.price),
                      ),
                    )}
                  </b>
                </button>
              ))}
            </div>
          </div>
          <div className="map-controls">
            <span>Select up to 8 seats</span>
            <div>
              <button
                className="icon-button"
                aria-label="Zoom out"
                disabled={zoom <= 0.8}
                onClick={() => setZoom((z) => z - 0.1)}
              >
                <Minus size={17} />
              </button>
              <span>{Math.round(zoom * 100)}%</span>
              <button
                className="icon-button"
                aria-label="Zoom in"
                disabled={zoom >= 1.5}
                onClick={() => setZoom((z) => z + 0.1)}
              >
                <Plus size={17} />
              </button>
              <button
                className="icon-button"
                aria-label={
                  list ? "Show seat map" : "Show accessible seat list"
                }
                onClick={() => setList(!list)}
              >
                {list ? <Grid2X2 size={18} /> : <List size={18} />}
              </button>
            </div>
          </div>
          {list ? (
            <div className="text-seat-list">
              {seats
                .filter((s) => section === "All" || s.section === section)
                .map((s) => (
                  <button
                    key={s.id}
                    disabled={s.status !== "AVAILABLE"}
                    className={selected.includes(s.id) ? "selected" : ""}
                    onClick={() => choose(s)}
                  >
                    <span>
                      {s.section} · Seat {s.row}
                      {s.number}
                    </span>
                    <span>
                      {money(s.price)} ·{" "}
                      {selected.includes(s.id)
                        ? "Selected"
                        : s.status.toLowerCase()}
                    </span>
                  </button>
                ))}
            </div>
          ) : (
            <div className="seat-map-scroll" ref={mapRef}>
              <div className="seat-map-inner" style={{ zoom }}>
                <div className="stage">
                  <span>THE STAGE</span>
                  <small>The moment starts here</small>
                </div>
                <div className="stage-front">FRONT OF STAGE</div>
                {rows.map((row) => (
                  <div className="seat-row" key={row}>
                    <span className="row-label">{row}</span>
                    <div className="seat-row-seats">
                      {seats
                        .filter((s) => s.row === row)
                        .map((s, i) => (
                          <button
                            key={s.id}
                            className={`seat ${s.section.toLowerCase()} ${s.status.toLowerCase()} ${selected.includes(s.id) ? "is-selected" : ""} ${i === Math.floor(data.venue.seatsPerRow / 2) ? "aisle" : ""}`}
                            disabled={s.status !== "AVAILABLE"}
                            aria-label={`Seat ${s.row}${s.number}, ${s.section}, ${money(s.price)}, ${selected.includes(s.id) ? "selected" : s.status.toLowerCase()}`}
                            aria-pressed={selected.includes(s.id)}
                            onClick={() => choose(s)}
                            onKeyDown={(e) => {
                              if (
                                ![
                                  "ArrowRight",
                                  "ArrowLeft",
                                  "ArrowUp",
                                  "ArrowDown",
                                ].includes(e.key)
                              )
                                return;
                              e.preventDefault();
                              const dir =
                                e.key === "ArrowRight"
                                  ? 1
                                  : e.key === "ArrowLeft"
                                    ? -1
                                    : e.key === "ArrowDown"
                                      ? data.venue.seatsPerRow
                                      : -data.venue.seatsPerRow;
                              const next =
                                mapRef.current?.querySelector<HTMLButtonElement>(
                                  `[data-seat="${s.id + dir}"]`,
                                );
                              if (next && !next.disabled) next.focus();
                            }}
                            data-seat={s.id}
                          >
                            {selected.includes(s.id) ? (
                              <Check size={13} />
                            ) : s.status === "AVAILABLE" ? (
                              s.number
                            ) : (
                              <span aria-hidden="true">×</span>
                            )}
                          </button>
                        ))}
                    </div>
                    <span className="row-label">{row}</span>
                  </div>
                ))}
              </div>
            </div>
          )}
          <div className="seat-legend">
            <span>
              <i className="legend-available" />
              Available
            </span>
            <span>
              <i className="legend-selected">
                <Check size={10} />
              </i>
              Your selection
            </span>
            <span>
              <i className="legend-held" />
              On hold
            </span>
            <span>
              <i className="legend-sold">×</i>Sold
            </span>
          </div>
          <div className="map-info">
            <Info size={16} />
            Selecting a seat doesn’t reserve it. Your 5-minute hold begins when
            you continue.
          </div>
          <span className="sr-only" aria-live="polite">
            {picked.length} seats selected. Subtotal {money(subtotal)}.
          </span>
        </section>
        <aside>
          <div className="panel seat-summary">
            <span className="eyebrow">YOUR NIGHT, YOUR SPOT</span>
            <h2>
              Your selection <span className="count-pill">{picked.length}</span>
            </h2>
            <div className="selected-seats">
              {picked.length ? (
                picked.map((s) => (
                  <div key={s.id}>
                    <span className="selected-seat-icon">
                      <Armchair size={18} />
                    </span>
                    <span>
                      <b>
                        {s.section} · {s.row}
                        {s.number}
                      </b>
                      <small>Reserved seating</small>
                    </span>
                    <strong>{money(s.price)}</strong>
                    <button
                      className="icon-button"
                      aria-label={`Remove seat ${s.row}${s.number}`}
                      onClick={() => choose(s)}
                    >
                      <Minus size={16} />
                    </button>
                  </div>
                ))
              ) : (
                <div className="no-seats">
                  <Armchair size={32} />
                  <p>
                    Pick your favorite spot
                    <br />
                    on the map to get started.
                  </p>
                </div>
              )}
            </div>
            <div className="price-breakdown">
              <p>
                <span>Tickets ({picked.length})</span>
                <b>{money(subtotal)}</b>
              </p>
              <p>
                <span>Service fee (5%)</span>
                <b>{money(fee)}</b>
              </p>
              <p className="price-total">
                <span>Total</span>
                <strong>{money(subtotal + fee)}</strong>
              </p>
            </div>
            <button
              className="button button-full"
              disabled={!picked.length || busy}
              onClick={reserve}
            >
              {busy ? (
                <>
                  <Loader2 className="spin" size={17} /> Reserving your seats…
                </>
              ) : (
                <>
                  Continue to checkout <Ticket size={18} />
                </>
              )}
            </button>
            <span className="booking-secure">
              <Timer size={15} /> We’ll hold your seats for 5 minutes
            </span>
          </div>
          <div className="help-note">
            <ShieldCheck size={19} />
            <p>
              Your seats are reserved together.
              <br />
              No split bookings. No surprises.
            </p>
          </div>
        </aside>
      </div>
    </div>
  );
}
export function WaitingRoom({ id }: { id: number }) {
  return (
    <Guard>
      <WaitingContent id={id} />
    </Guard>
  );
}
function WaitingContent({ id }: { id: number }) {
  const [state, setState] = useState<{
      status: string;
      position: number;
      admissionToken?: string;
    }>(),
    [error, setError] = useState("");
  useEffect(() => {
    let stop = false;
    async function update() {
      try {
        const result = await api<typeof state>(`/events/${id}/queue`, {
          method: "POST",
        });
        if (stop) return;
        setState(result);
        if (result?.admissionToken)
          sessionStorage.setItem(
            "eventhub-admission-" + id,
            result.admissionToken,
          );
      } catch (e) {
        if (!stop) setError((e as Error).message);
      }
    }
    void update();
    const timer = setInterval(update, 5000);
    return () => {
      stop = true;
      clearInterval(timer);
    };
  }, [id]);
  return (
    <div className="container page waiting-room">
      <span className="waiting-icon">
        <Users size={44} />
      </span>
      <span className="eyebrow">GOOD THINGS ARE WORTH THE WAIT</span>
      <h1>
        {state?.status === "ADMITTED"
          ? "Your moment is here."
          : "You’re in the right place."}
      </h1>
      <p>
        We’re welcoming fans in order so everyone has a smooth booking
        experience.
      </p>
      {error ? (
        <ErrorState message={error} retry={() => location.reload()} />
      ) : !state ? (
        <Loading text="Joining the waiting room…" />
      ) : state.status === "ADMITTED" ? (
        <div className="panel">
          <Check size={35} />
          <h2>You’re ready to choose your seats</h2>
          <p>
            Your admission is valid for 10 minutes. A ticket isn’t reserved
            until you choose and hold your seats.
          </p>
          <Link className="button" href={`/events/${id}/seats`}>
            Choose seats <Ticket size={17} />
          </Link>
        </div>
      ) : (
        <div className="panel">
          <span>Your place in line</span>
          <strong className="queue-position">{state.position}</strong>
          <div className="queue-pulse" />
          <p>
            This page updates automatically. Keep it open to maintain your
            place.
          </p>
        </div>
      )}
      <Link href={`/events/${id}`} className="text-link">
        Back to event details
      </Link>
    </div>
  );
}
