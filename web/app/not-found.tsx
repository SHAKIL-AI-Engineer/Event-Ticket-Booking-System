import Link from "next/link";
export default function NotFound() {
  return (
    <div className="container page empty-state">
      <span className="eyebrow">404 · A little off the beaten path</span>
      <h1>This moment isn’t here.</h1>
      <p>There are plenty of great ones waiting for you.</p>
      <Link href="/events" className="button">
        Explore events
      </Link>
    </div>
  );
}
