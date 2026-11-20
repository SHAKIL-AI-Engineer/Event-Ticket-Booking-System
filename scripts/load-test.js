// Optional k6 contested-seat test against an isolated, authorized test deployment.
// Supply independently authenticated session cookies in EVENTHUB_SESSION_COOKIES (JSON array).
// Never run against live customer inventory.
import http from "k6/http";
import { check } from "k6";
import { Counter, Trend } from "k6/metrics";
const cookies = JSON.parse(__ENV.EVENTHUB_SESSION_COOKIES || "[]");
if (cookies.length < 2)
  throw new Error("Supply at least two independent test-user session cookies.");
const successes = new Counter("holds_success");
const latency = new Trend("holds_latency", true);
export const options = {
  vus: Number(__ENV.VUS || 16),
  iterations: Number(__ENV.ITERATIONS || 16),
  thresholds: {
    holds_success: ["count==1"],
    checks: ["rate==1"],
    holds_latency: [`p(95)<${Number(__ENV.HOLD_P95_MS || 500)}`],
  },
};
export default function () {
  const base = __ENV.EVENTHUB_BASE_URL || "http://localhost:5050";
  const userCookie = cookies[(__VU - 1) % cookies.length];
  const response = http.post(
    `${base}/api/v1/holds`,
    JSON.stringify({
      eventId: Number(__ENV.EVENT_ID || 1),
      seatIds: [Number(__ENV.SEAT_ID || 1)],
      admissionToken: __ENV.ADMISSION_TOKEN || null,
    }),
    {
      headers: {
        "Content-Type": "application/json",
        Cookie: `eventhub_session=${userCookie}`,
        "Idempotency-Key": `k6-${__VU}-${__ITER}-${Date.now()}`,
      },
    },
  );
  latency.add(response.timings.duration);
  if (response.status === 201) successes.add(1);
  check(response, {
    "hold wins or reports a seat conflict": (r) =>
      r.status === 201 || r.status === 409,
  });
}
