import Link from "next/link";
export const metadata = { title: "About EventHub" };
export default function Page() {
  return (
    <div className="container page">
      <article className="help-content prose">
        <div className="page-title">
          <span className="eyebrow">FOR THE LOVE OF BEING THERE</span>
          <h1>Life happens live.</h1>
          <p>And we think you should be there for the good stuff.</p>
        </div>
        <div className="about-hero">
          <img
            src="/images/festival.jpg"
            alt="A crowd coming together at a music festival"
          />
        </div>
        <h2>The stories you’ll tell start with showing up.</h2>
        <p>
          The song that gives you goosebumps. The joke you can’t stop laughing
          at. The stranger beside you who suddenly feels like an old friend.
          Some things just don’t happen through a screen.
        </p>
        <p>
          EventHub brings live experiences across Bangladesh into one place, so
          you can spend less time making plans and more time making memories.
          Find your kind of event, choose your favorite seat, and keep your
          ticket ready for the moment.
        </p>
        <h2>Built for the people who make it happen.</h2>
        <p>
          Every great event starts with an idea and someone willing to bring it
          to life. Our organizer workspace connects event details, seat pricing,
          bookings, and venue entry — giving organizers more time to focus on
          the experience.
        </p>
        <div className="help-callout">
          <h3>Your next great story is out there.</h3>
          <p>
            <Link className="button" href="/events">
              Find your next moment
            </Link>
          </p>
        </div>
      </article>
    </div>
  );
}
