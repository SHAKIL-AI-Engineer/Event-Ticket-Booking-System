export class ApiError extends Error {
  constructor(
    public status: number,
    public code: string,
    message: string,
    public details?: unknown,
  ) {
    super(message);
  }
}
export async function api<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  const mutating = options.method && options.method !== "GET";
  const headers = new Headers(options.headers);
  if (options.body) headers.set("Content-Type", "application/json");
  if (mutating && !headers.has("Idempotency-Key"))
    headers.set("Idempotency-Key", crypto.randomUUID());
  const response = await fetch(`/api/v1${path}`, {
    ...options,
    headers,
    credentials: "same-origin",
    cache: "no-store",
  });
  const content = await response.json().catch(() => ({}));
  if (!response.ok)
    throw new ApiError(
      response.status,
      content.code || "ERROR",
      content.title || "Something went wrong. Please try again.",
      content.details,
    );
  return content as T;
}
export const money = (minor: number) =>
  new Intl.NumberFormat("en-BD", {
    style: "currency",
    currency: "BDT",
    maximumFractionDigits: minor % 100 ? 2 : 0,
  }).format(minor / 100);
export const date = (iso: string, options?: Intl.DateTimeFormatOptions) =>
  new Intl.DateTimeFormat("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
    timeZone: "Asia/Dhaka",
    ...options,
  }).format(new Date(utc(iso)));
export const time = (iso: string) =>
  date(iso, {
    day: undefined,
    month: undefined,
    year: undefined,
    hour: "numeric",
    minute: "2-digit",
    hour12: true,
  });
export const utc = (iso: string) =>
  iso.endsWith("Z") || /[+-]\d\d:\d\d$/.test(iso) ? iso : iso + "Z";
export const json = (value: unknown) => JSON.stringify(value);
