"use client";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useState } from "react";
import {
  Ticket,
  Menu,
  X,
  MapPin,
  ChevronDown,
  LogOut,
  UserRound,
  LayoutDashboard,
  ShieldCheck,
  ScanLine,
  Bell,
  Heart,
  Instagram,
  Music2,
} from "lucide-react";
import { useApp } from "./providers";
import { api } from "@/lib/api";
export function Brand() {
  return (
    <Link href="/" className="brand" aria-label="EventHub home">
      <span className="brand-mark">
        <Ticket size={22} strokeWidth={2.3} />
      </span>
      eventhub<span className="brand-dot">.</span>
    </Link>
  );
}
export function Header() {
  const { user, refresh, notify } = useApp(),
    [menu, setMenu] = useState(false),
    [profile, setProfile] = useState(false),
    path = usePathname(),
    router = useRouter();
  async function logout() {
    try {
      await api("/auth/logout", { method: "POST" });
      await refresh();
      setProfile(false);
      router.push("/");
      notify("You’ve signed out.");
    } catch (e) {
      notify((e as Error).message, true);
    }
  }
  return (
    <header className="header">
      <div className="container header-inner">
        <Brand />
        <nav
          className={menu ? "nav nav-open" : "nav"}
          aria-label="Main navigation"
        >
          <Link
            className={path === "/" || path === "/events" ? "active" : ""}
            href="/events"
            onClick={() => setMenu(false)}
          >
            Discover events
          </Link>
          <Link
            className={path === "/tickets" ? "active" : ""}
            href="/tickets"
            onClick={() => setMenu(false)}
          >
            My tickets
          </Link>
          <Link
            className={path.startsWith("/organizer") ? "active" : ""}
            href="/organizer"
            onClick={() => setMenu(false)}
          >
            For organizers
          </Link>
        </nav>
        <div className="header-actions">
          <span className="location-label">
            <MapPin size={15} /> Bangladesh
          </span>
          {user ? (
            <>
              <Link
                className="icon-button notification-link"
                href="/account?tab=notifications"
                aria-label="Notifications"
              >
                <Bell size={19} />
              </Link>
              <div className="profile-wrap">
                <button
                  className="avatar"
                  onClick={() => setProfile(!profile)}
                  aria-expanded={profile}
                  aria-label="Account menu"
                >
                  {user.name
                    .split(" ")
                    .map((x) => x[0])
                    .slice(0, 2)
                    .join("")}
                </button>
                {profile && (
                  <>
                    <button
                      className="menu-backdrop"
                      onClick={() => setProfile(false)}
                      aria-label="Close account menu"
                    />
                    <div className="profile-menu">
                      <strong>{user.name}</strong>
                      <small>{user.email}</small>
                      <Link href="/account" onClick={() => setProfile(false)}>
                        <UserRound size={17} /> My account
                      </Link>
                      <Link href="/saved" onClick={() => setProfile(false)}>
                        <Heart size={17} /> Saved events
                      </Link>
                      {["Organizer", "Admin"].includes(user.role) && (
                        <Link
                          href="/organizer"
                          onClick={() => setProfile(false)}
                        >
                          <LayoutDashboard size={17} /> Organizer dashboard
                        </Link>
                      )}
                      {user.role === "Admin" && (
                        <Link href="/admin" onClick={() => setProfile(false)}>
                          <ShieldCheck size={17} /> Admin operations
                        </Link>
                      )}
                      {["Staff", "Organizer", "Admin"].includes(user.role) && (
                        <Link
                          href="/check-in"
                          onClick={() => setProfile(false)}
                        >
                          <ScanLine size={17} /> Venue check-in
                        </Link>
                      )}
                      <button onClick={logout}>
                        <LogOut size={17} /> Sign out
                      </button>
                    </div>
                  </>
                )}
              </div>
            </>
          ) : (
            <Link className="button button-small" href="/login">
              Sign in <UserRound size={15} />
            </Link>
          )}
          <button
            className="icon-button mobile-menu"
            onClick={() => setMenu(!menu)}
            aria-label={menu ? "Close navigation" : "Open navigation"}
          >
            {menu ? <X /> : <Menu />}
          </button>
        </div>
      </div>
    </header>
  );
}
export function Footer() {
  return (
    <footer className="footer">
      <div className="container">
        <div className="footer-top">
          <div>
            <Brand />
            <p>
              Less scrolling. More living.
              <br />
              Find your next unforgettable moment.
            </p>
          </div>
          <div>
            <h4>Explore</h4>
            <Link href="/events">Discover events</Link>
            <Link href="/events?category=Music">Live music</Link>
            <Link href="/events?category=Arts+%26+Theatre">Arts & theatre</Link>
          </div>
          <div>
            <h4>Your experience</h4>
            <Link href="/tickets">My tickets</Link>
            <Link href="/saved">Saved events</Link>
            <Link href="/help">Help & support</Link>
          </div>
          <div>
            <h4>Make it happen</h4>
            <Link href="/organizer">Organize an event</Link>
            <Link href="/about">About EventHub</Link>
            <Link href="/policies">Ticket policies</Link>
          </div>
        </div>
        <div className="footer-bottom">
          <span>
            © {new Date().getFullYear()} EventHub. Made for moments that matter.
          </span>
          <span>
            Dhaka, Bangladesh <span className="tiny-star">✳</span>
          </span>
        </div>
      </div>
    </footer>
  );
}
