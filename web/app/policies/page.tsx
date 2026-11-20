export const metadata = { title: "Ticket & refund policies" };
export default function Page() {
  return (
    <div className="container page">
      <article className="help-content prose">
        <div className="page-title">
          <span className="eyebrow">CLEAR PLANS. NO SURPRISES.</span>
          <h1>A few things to know.</h1>
          <p>Ticket, booking, and refund policies for EventHub.</p>
        </div>
        <h2>Booking and prices</h2>
        <p>
          All prices are shown in Bangladeshi taka (BDT). A 5% service fee is
          shown before payment. The server-generated quote at the time of your
          hold determines your total. You can reserve or own up to 8 tickets per
          event. Seat groups are reserved together.
        </p>
        <h2>Your five-minute hold</h2>
        <p>
          Seats are held for 5 minutes after the reservation is confirmed. The
          server’s UTC clock determines expiry. Selecting a seat on the map does
          not reserve it. If the hold expires before payment authorization,
          choose your seats again.
        </p>
        <h2>Payment and confirmation</h2>
        <p>
          A booking is confirmed when payment capture is verified and tickets
          are issued. If the outcome is unknown, the order remains processing
          while the system reconciles it. Check your existing order before
          attempting another booking. Sandbox transactions do not charge real
          money.
        </p>
        <h2>Refunds and cancellations</h2>
        <p>
          Refunds may be requested more than 24 hours before the scheduled
          event. All tickets in the order must be unused and untransferred.
          Refunds require administrator review. During review, check-in and
          transfers are paused. A rejection restores the confirmed order; an
          approved and settled refund revokes tickets and releases seats. If an
          organizer cancels an event, refund requests are created for confirmed
          orders and await review.
        </p>
        <h2>Ticket transfers</h2>
        <p>
          A valid ticket may be transferred once before the event starts. The
          recipient must have an EventHub account. Transfers are final,
          invalidate the previous QR code, and make the original order
          ineligible for the standard self-service refund.
        </p>
        <h2>Venue entry</h2>
        <p>
          Present the original QR code at the correct event. Bring valid photo
          identification where the venue requires it. Check-in opens 2 hours
          before the event and closes when it ends. A ticket admits one person
          once. Online ticket validation is required; offline scanning is not
          supported.
        </p>
        <h2>Your data</h2>
        <p>
          EventHub stores the account and booking details needed to provide your
          tickets. Passwords are hashed and sessions use an HTTP-only cookie.
          Card details are entered through Stripe when live payments are
          configured. Event organizers can access their attendee lists; venue
          staff can validate tickets. Keep QR codes and account passwords
          private.
        </p>
      </article>
    </div>
  );
}
