export type User = {
  id: string;
  name: string;
  email: string;
  role: "Customer" | "Organizer" | "Admin" | "Staff";
};
export type Venue = {
  id: number;
  name: string;
  city: string;
  address: string;
  timeZone: string;
  rows: number;
  seatsPerRow: number;
};
export type Event = {
  id: number;
  title: string;
  slug: string;
  category: string;
  artist: string;
  description: string;
  image: string;
  venueId: number;
  organizerId: string;
  startsAt: string;
  endsAt: string;
  status: string;
  featured: boolean;
  queueEnabled: boolean;
  minPrice: number;
  currency: string;
  policies: string;
};
export type EventItem = {
  ev: Event;
  venue: Venue;
  capacity?: number;
  sold?: number;
  available?: number;
};
export type Seat = {
  id: number;
  row: string;
  number: number;
  section: string;
  price: number;
  status: string;
  version: number;
};
export type Hold = {
  id: string;
  eventId: number;
  status: string;
  expiresAt: string;
  serverTime: string;
  subtotal: number;
  fee: number;
  total: number;
  seats: Seat[];
};
export type Order = {
  id: string;
  holdId: string;
  userId: string;
  eventId: number;
  status: string;
  subtotal: number;
  fee: number;
  total: number;
  currency: string;
  createdAt: string;
};
export type Ticket = {
  id: string;
  orderId: string;
  userId: string;
  eventId: number;
  seatId: number;
  status: string;
  isActive: boolean;
  transferCount: number;
  checkedInAt?: string;
  gate?: string;
};
export type TicketItem = {
  ticket: Ticket;
  ev: Event;
  venue: Venue;
  seat: Seat;
  token: string | null;
};
export type Refund = {
  id: string;
  orderId: string;
  amount: number;
  reason: string;
  status: string;
  createdAt: string;
};
export type OrderView = {
  order: Order;
  ev: Event;
  venue: Venue;
  seats: Seat[];
  tickets: Ticket[];
  refund: Refund | null;
  payment: {
    id: string;
    status: string;
    provider: string;
    error?: string;
    clientSecret?: string;
  } | null;
};
export type Config = {
  paymentProvider: string;
  stripePublishableKey?: string;
  demoAvailable: boolean;
  currency: string;
  holdSeconds: number;
};
