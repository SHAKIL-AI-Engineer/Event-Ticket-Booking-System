import type { Metadata } from "next";
import { Providers } from "@/components/providers";
import { Header, Footer } from "@/components/shell";
import "./globals.css";
export const metadata: Metadata = {
  title: {
    default: "EventHub — Find your next unforgettable moment",
    template: "%s | EventHub",
  },
  description:
    "Discover live music, comedy, theatre, and more in Bangladesh. Choose your seats, book securely, and keep every ticket in one place.",
};
export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body>
        <Providers>
          <a className="skip-link" href="#main">
            Skip to content
          </a>
          <Header />
          <main id="main">{children}</main>
          <Footer />
        </Providers>
      </body>
    </html>
  );
}
