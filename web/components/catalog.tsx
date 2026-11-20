"use client";
import { useEffect, useState, Suspense } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import {
  Heart,
  MapPin,
  CalendarDays,
  Search,
  SlidersHorizontal,
  Music2,
  Mic2,
  Theater,
  Utensils,
  Monitor,
  Dribbble,
  Compass,
  ChevronDown,
  Grid2X2,
  List,
  Ticket,
  AudioLines,
  ShieldCheck,
  ScanLine,
  Sparkles,
  MoveUpRight,
} from "lucide-react";
import { motion } from "motion/react";
import { api, date, money } from "@/lib/api";
import type { EventItem, Venue } from "@/lib/types";
import { useApp } from "./providers";
import { Empty, ErrorState, Loading } from "./ui";
export const categories = [
  { name: "All events", value: "All", icon: Compass },
  { name: "Music", value: "Music", icon: Music2 },
  { name: "Comedy", value: "Comedy", icon: Mic2 },
  { name: "Arts & theatre", value: "Arts & Theatre", icon: Theater },
  { name: "Technology", value: "Technology", icon: Monitor },
  { name: "Food & culture", value: "Food & Culture", icon: Utensils },
  { name: "Sports", value: "Sports", icon: Dribbble },
];
export function EventCard({
  item,
  index = 0,
}: {
  item: EventItem;
  index?: number;
}) {
  const { ev, venue } = item,
    { favorites, toggleFavorite } = useApp();
  return (
    <motion.article
      className="event-card"
      initial={{ opacity: 0, y: 12 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true }}
      transition={{ duration: 0.3, delay: Math.min(index * 0.04, 0.2) }}
    >
      <div className="event-cover">
        <Link href={`/events/${ev.id}`} aria-label={`View ${ev.title}`}>
          <img
            src={ev.image}
            alt={`${ev.category} event: ${ev.title}`}
            loading="lazy"
          />
        </Link>
        <span className="cover-category">{ev.category}</span>
        <button
          className={`favorite-button ${favorites.includes(ev.id) ? "is-saved" : ""}`}
          aria-label={`${favorites.includes(ev.id) ? "Unsave" : "Save"} ${ev.title}`}
          aria-pressed={favorites.includes(ev.id)}
          onClick={() => toggleFavorite(ev.id)}
        >
          <Heart
            size={18}
            fill={favorites.includes(ev.id) ? "currentColor" : "none"}
          />
        </button>
        <div className="date-chip">
          <b>
            {date(ev.startsAt, {
              day: "2-digit",
              month: undefined,
              year: undefined,
            })}
          </b>
          <span>
            {date(ev.startsAt, {
              day: undefined,
              month: "short",
              year: undefined,
            })}
          </span>
        </div>
      </div>
      <div className="event-card-body">
        <span className="event-artist">{ev.artist}</span>
        <h3>
          <Link href={`/events/${ev.id}`}>{ev.title}</Link>
        </h3>
        <p>
          <MapPin size={14} />
          {venue.name}, {venue.city}
        </p>
        <div className="event-card-bottom">
          <span>
            From <strong>{money(ev.minPrice)}</strong>
          </span>
          <Link href={`/events/${ev.id}`} className="text-link">
            Get tickets <Ticket size={15} />
          </Link>
        </div>
      </div>
    </motion.article>
  );
}
export function SearchBar({
  initial = "",
  city: initialCity = "All cities",
  category: initialCategory = "All",
}: {
  initial?: string;
  city?: string;
  category?: string;
}) {
  const router = useRouter(),
    [q, setQ] = useState(initial),
    [city, setCity] = useState(initialCity),
    [category, setCategory] = useState(initialCategory),
    [cities, setCities] = useState(["Dhaka", "Chattogram"]);
  useEffect(() => {
    api<Venue[]>("/venues")
      .then((v) => setCities([...new Set(v.map((x) => x.city))]))
      .catch(() => {});
  }, []);
  return (
    <form
      className="search-bar"
      onSubmit={(e) => {
        e.preventDefault();
        const params = new URLSearchParams();
        if (q) params.set("q", q);
        if (city !== "All cities") params.set("city", city);
        if (category !== "All") params.set("category", category);
        router.push("/events?" + params);
      }}
    >
      <label className="search-text">
        <Search size={21} />
        <span>
          <b>What’s your next plan?</b>
          <input
            aria-label="Search events or artists"
            placeholder="Search events, artists, or venues"
            value={q}
            onChange={(e) => setQ(e.target.value)}
          />
        </span>
      </label>
      <label>
        <MapPin size={20} />
        <span>
          <b>Where</b>
          <select
            aria-label="Choose city"
            value={city}
            onChange={(e) => setCity(e.target.value)}
          >
            <option>All cities</option>
            {cities.map((c) => (
              <option key={c}>{c}</option>
            ))}
          </select>
        </span>
        <ChevronDown size={14} />
      </label>
      <label>
        <CalendarDays size={20} />
        <span>
          <b>Your kind of thing</b>
          <select
            aria-label="Choose event category"
            value={category}
            onChange={(e) => setCategory(e.target.value)}
          >
            {categories.map((c) => (
              <option key={c.value} value={c.value}>
                {c.name}
              </option>
            ))}
          </select>
        </span>
        <ChevronDown size={14} />
      </label>
      <button className="button" type="submit">
        <Search size={18} /> Find events
      </button>
    </form>
  );
}
export function Home() {
  const [items, setItems] = useState<EventItem[]>([]),
    [loading, setLoading] = useState(true),
    [error, setError] = useState(""),
    [category, setCategory] = useState("All");
  useEffect(() => {
    api<{ items: EventItem[] }>("/events")
      .then((r) => setItems(r.items))
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  }, []);
  const featured = items.find((x) => x.ev.featured) || items[0];
  return (
    <>
      <section className="hero-section">
        <div className="container">
          <div className="hero">
            <img
              className="hero-image"
              src="/images/concert.jpg"
              alt="A crowd sharing a night of live music under colorful stage lights"
            />
            <div className="hero-shade" />
            <div className="hero-content">
              <span className="hero-kicker">
                <span /> GO OUT. FEEL SOMETHING.
              </span>
              <h1>
                Good times.
                <br />
                Great <em>stories.</em>
              </h1>
              <p>
                The live moments you’ll talk about for years.
                <br />
                Find them here. Be there for them.
              </p>
              <Link href="/events" className="button button-lime">
                Find your next moment <Ticket size={18} />
              </Link>
              <div className="hero-bottom-note">
                <AudioLines size={20} />
                <span>For the love of being there.</span>
              </div>
            </div>
            {featured && (
              <Link
                href={`/events/${featured.ev.id}`}
                className="featured-ticket"
              >
                <span className="featured-ticket-top">
                  <Sparkles size={14} /> IN THE SPOTLIGHT
                </span>
                <h3>{featured.ev.title}</h3>
                <span>
                  {date(featured.ev.startsAt, { year: undefined })} <i>·</i>{" "}
                  {featured.venue.city}
                </span>
                <div>
                  <b>From {money(featured.ev.minPrice)}</b>
                  <span className="featured-icon">
                    <Ticket size={19} />
                  </span>
                </div>
              </Link>
            )}
            <span className="hero-vertical">
              FIND YOUR PEOPLE. MAKE A MEMORY.
            </span>
          </div>
          <SearchBar />
        </div>
      </section>
      <section className="container discovery-section">
        <div className="section-heading">
          <div>
            <span className="eyebrow">A LITTLE SOMETHING FOR EVERYONE</span>
            <h2>What moves you?</h2>
          </div>
          <p>Your people. Your passions. Your next plan.</p>
        </div>
        <div className="category-tabs">
          {categories.map((c) => (
            <button
              key={c.value}
              className={
                category === c.value ? "category-tab selected" : "category-tab"
              }
              onClick={() => setCategory(c.value)}
            >
              <c.icon size={19} />
              {c.name}
            </button>
          ))}
        </div>
        <div className="section-heading events-heading">
          <div>
            <h2>
              {category === "All"
                ? "Worth stepping out for"
                : category + " worth stepping out for"}
              <span className="title-spark">✳</span>
            </h2>
            <p>Big nights, new discoveries, and everything in between.</p>
          </div>
          <Link
            href={
              category === "All"
                ? "/events"
                : "/events?category=" + encodeURIComponent(category)
            }
            className="button button-outline button-small"
          >
            View all events
          </Link>
        </div>
        {error ? (
          <ErrorState message={error} retry={() => location.reload()} />
        ) : loading ? (
          <div className="event-grid">
            {[0, 1, 2, 3].map((x) => (
              <div className="card-skeleton" key={x} />
            ))}
          </div>
        ) : (
          <div className="event-grid">
            {items
              .filter((x) => category === "All" || x.ev.category === category)
              .slice(0, 6)
              .map((x, i) => (
                <EventCard key={x.ev.id} item={x} index={i} />
              ))}
          </div>
        )}
        {!loading &&
          !error &&
          items.filter((x) => category === "All" || x.ev.category === category)
            .length === 0 && (
            <Empty
              title="Something good is on its way"
              text="There aren’t any upcoming events in this category yet. Explore something new."
              action="See all events"
              href="/events"
            />
          )}
      </section>
      <section className="container">
        <div className="experience-strip">
          <div>
            <span className="benefit-icon">
              <Ticket />
            </span>
            <h3>A seat with your name on it</h3>
            <p>Choose your spot with live seat availability.</p>
          </div>
          <div>
            <span className="benefit-icon">
              <ShieldCheck />
            </span>
            <h3>Good plans. No guesswork.</h3>
            <p>Clear prices and a secure checkout, always.</p>
          </div>
          <div>
            <span className="benefit-icon">
              <ScanLine />
            </span>
            <h3>Less waiting. More living.</h3>
            <p>Your tickets, ready on your phone at the door.</p>
          </div>
        </div>
      </section>
      <section className="container organizer-promo">
        <div>
          <span className="eyebrow">
            YOU BRING THE IDEA. WE BRING THE PEOPLE.
          </span>
          <h2>
            Make your next event
            <br />
            their next great story.
          </h2>
          <p>
            From the first ticket to the final encore.
            <br />A better way to bring people together.
          </p>
          <Link href="/organizer" className="button">
            Start organizing <Ticket size={17} />
          </Link>
        </div>
        <div className="promo-visual" aria-hidden="true">
          <span className="promo-star">✳</span>
          <div className="paper-ticket">
            <span>EVENTHUB PRESENTS</span>
            <h3>
              Your next
              <br />
              big thing.
            </h3>
            <div className="ticket-perforation" />
            <div className="paper-ticket-bottom">
              <span>
                ALL THE POSSIBILITIES
                <br />
                <b>ONE GREAT EVENT</b>
              </span>
              <Ticket size={38} />
            </div>
          </div>
          <span className="promo-caption">Big ideas deserve a full house.</span>
        </div>
      </section>
    </>
  );
}
export function Discover() {
  return (
    <Suspense fallback={<Loading />}>
      <DiscoveryContent />
    </Suspense>
  );
}
function DiscoveryContent() {
  const params = useSearchParams(),
    router = useRouter(),
    [result, setResult] = useState<{
      items: EventItem[];
      total: number;
      page: number;
    }>({ items: [], total: 0, page: 1 }),
    [loading, setLoading] = useState(true),
    [error, setError] = useState(""),
    [filters, setFilters] = useState(false),
    [list, setList] = useState(false);
  const query = params.toString();
  useEffect(() => {
    const ctrl = new AbortController();
    setLoading(true);
    setError("");
    api<typeof result>("/events?" + query, { signal: ctrl.signal })
      .then(setResult)
      .catch((e) => {
        if (e.name !== "AbortError") setError(e.message);
      })
      .finally(() => {
        if (!ctrl.signal.aborted) setLoading(false);
      });
    return () => ctrl.abort();
  }, [query]);
  function change(key: string, value: string) {
    const p = new URLSearchParams(query);
    if (value && value !== "All") p.set(key, value);
    else p.delete(key);
    if (key !== "page") p.delete("page");
    router.push("/events?" + p, { scroll: false });
  }
  return (
    <div className="container page">
      <div className="page-title">
        <span className="eyebrow">LESS ROUTINE. MORE REMEMBER-WHEN.</span>
        <h1>
          Find your next <em>moment.</em>
        </h1>
        <p>A whole world of live experiences, right here.</p>
      </div>
      <SearchBar
        key={query.replace(/&?page=\d+/, "")}
        initial={params.get("q") || ""}
        city={params.get("city") || "All cities"}
        category={params.get("category") || "All"}
      />
      <div className="category-tabs discovery-tabs">
        {categories.map((c) => (
          <button
            key={c.value}
            className={
              (params.get("category") || "All") === c.value
                ? "category-tab selected"
                : "category-tab"
            }
            onClick={() => change("category", c.value)}
          >
            <c.icon size={18} />
            {c.name}
          </button>
        ))}
      </div>
      <div className="results-bar">
        <span>
          <strong>{result.total}</strong> experiences to make your own
        </span>
        <div>
          <button
            className="button button-outline button-small"
            onClick={() => setFilters(!filters)}
            aria-expanded={filters}
          >
            <SlidersHorizontal size={16} /> Filters
          </button>
          <select
            className="sort-select"
            aria-label="Sort events"
            value={params.get("sort") || "date"}
            onChange={(e) => change("sort", e.target.value)}
          >
            <option value="date">Soonest first</option>
            <option value="price">Price: low to high</option>
          </select>
          <div className="view-toggle">
            <button
              aria-label="Grid view"
              aria-pressed={!list}
              onClick={() => setList(false)}
            >
              <Grid2X2 size={17} />
            </button>
            <button
              aria-label="List view"
              aria-pressed={list}
              onClick={() => setList(true)}
            >
              <List size={17} />
            </button>
          </div>
        </div>
      </div>
      {filters && (
        <div className="filter-panel">
          <label>
            From date
            <input
              type="date"
              value={params.get("from") || ""}
              onChange={(e) => change("from", e.target.value)}
            />
          </label>
          <label>
            Until date
            <input
              type="date"
              value={params.get("to") || ""}
              onChange={(e) => change("to", e.target.value)}
            />
          </label>
          <label>
            Maximum ticket price
            <select
              value={params.get("maxPrice") || ""}
              onChange={(e) => change("maxPrice", e.target.value)}
            >
              <option value="">Any price</option>
              <option value="100000">Under ৳1,000</option>
              <option value="200000">Under ৳2,000</option>
              <option value="300000">Under ৳3,000</option>
              <option value="500000">Under ৳5,000</option>
            </select>
          </label>
          <button className="text-link" onClick={() => router.push("/events")}>
            Clear filters
          </button>
        </div>
      )}
      {loading ? (
        <div className="event-grid">
          {[0, 1, 2, 3, 4, 5].map((x) => (
            <div className="card-skeleton" key={x} />
          ))}
        </div>
      ) : error ? (
        <ErrorState message={error} retry={() => location.reload()} />
      ) : result.items.length ? (
        <div className={list ? "event-grid event-list" : "event-grid"}>
          {result.items.map((x, i) => (
            <EventCard key={x.ev.id} item={x} index={i} />
          ))}
        </div>
      ) : (
        <Empty
          title="No moments found. Yet."
          text="Try a different search, city, or date. Your next great plan is out there."
          action="Clear all filters"
          href="/events"
        />
      )}
      {result.total > 12 && (
        <div className="pagination">
          <button
            className="button button-outline"
            disabled={result.page === 1}
            onClick={() => change("page", String(result.page - 1))}
          >
            Previous
          </button>
          <span>
            Page {result.page} of {Math.ceil(result.total / 12)}
          </span>
          <button
            className="button button-outline"
            disabled={result.page * 12 >= result.total}
            onClick={() => change("page", String(result.page + 1))}
          >
            Next
          </button>
        </div>
      )}
    </div>
  );
}
export function SavedEvents() {
  const { favorites, user, loading: authLoading } = useApp(),
    [items, setItems] = useState<EventItem[]>([]),
    [loading, setLoading] = useState(true);
  useEffect(() => {
    if (authLoading) return;
    let cancelled = false;
    Promise.all(
      favorites.map((id) => api<EventItem>("/events/" + id).catch(() => null)),
    ).then((data) => {
      if (!cancelled) {
        setItems(data.filter((x): x is EventItem => x !== null));
        setLoading(false);
      }
    });
    return () => {
      cancelled = true;
    };
  }, [favorites, authLoading]);
  return (
    <div className="container page">
      <div className="page-title">
        <span className="eyebrow">A LITTLE LIST OF BIG POSSIBILITIES</span>
        <h1>Your saved events.</h1>
        <p>Keep the good plans close.</p>
      </div>
      {authLoading || loading ? (
        <Loading />
      ) : !user ? (
        <Empty
          title="Save the ones you love"
          text="Sign in to build a little list of experiences you don’t want to miss."
          href="/login"
          action="Sign in"
        />
      ) : items.length ? (
        <div className="event-grid">
          {items.map((x) => (
            <EventCard key={x.ev.id} item={x} />
          ))}
        </div>
      ) : (
        <Empty
          title="Your next plan starts here"
          text="Tap the heart on any event to save it for later."
          href="/events"
          action="Explore events"
        />
      )}
    </div>
  );
}
