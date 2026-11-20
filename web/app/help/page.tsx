import Link from "next/link";
export const metadata = { title: "Help & support" };
const faqs = [
  [
    "How do I book tickets?",
    "Find your event, choose your seats, then continue to checkout. Your selected seats are held for 5 minutes while you complete payment. A selection on the map becomes a reservation only after the server confirms the hold.",
  ],
  [
    "Where are my tickets?",
    "Open My tickets after your order is confirmed. Each ticket has a unique QR code for venue entry. You can save the QR image or add the event to your calendar. Keep your QR code private.",
  ],
  [
    "What if my payment is still processing?",
    "Open your order from Order history to check its latest status. EventHub checks the existing payment attempt automatically. Avoid creating another booking while your payment is being verified. You can safely close the page and come back.",
  ],
  [
    "Can I transfer a ticket?",
    "Yes, a valid ticket can be transferred once to another registered EventHub account before the event starts. Open your ticket and select Transfer to a friend. Transfers invalidate the previous QR code and cannot be undone.",
  ],
  [
    "How do refunds work?",
    "Request a refund from your confirmed order at least 24 hours before the event. Tickets must be unused and untransferred. An administrator reviews the request. Tickets are paused during the review and revoked only after the provider confirms the refund.",
  ],
  [
    "What happens if my hold expires?",
    "Your seats become available for other fans. Return to the seat map to choose available seats and start a new hold. The server determines expiry, so a browser countdown cannot extend a reservation.",
  ],
  [
    "Why do I see a sandbox checkout?",
    "This installation is using the sandbox payment provider. No real money is charged. Successful, declined, and delayed payments are available to explore the booking workflow. An administrator must configure Stripe keys for real payments.",
  ],
  [
    "Can I organize my own event?",
    "Yes. Create an account, open For organizers, and choose Become an organizer. You can create a venue, publish events, set seat prices, export your attendee list, and validate tickets.",
  ],
];
export default function Page() {
  return (
    <div className="container page">
      <div className="help-content">
        <div className="page-title">
          <span className="eyebrow">A LITTLE HELP GOES A LONG WAY</span>
          <h1>Let’s keep the good times going.</h1>
          <p>
            Answers to the little questions between you and your next great
            moment.
          </p>
        </div>
        {faqs.map(([q, a]) => (
          <details className="faq-item" key={q}>
            <summary>{q}</summary>
            <p>{a}</p>
          </details>
        ))}
        <div className="help-callout">
          <h3>Looking for a booking update?</h3>
          <p>
            Your order page has the latest payment, ticket, and refund status.{" "}
            <Link className="text-link" href="/tickets?tab=orders">
              Open your order history
            </Link>
          </p>
        </div>
      </div>
    </div>
  );
}
