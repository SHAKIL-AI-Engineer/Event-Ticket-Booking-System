import type { NextConfig } from "next";
const backend = process.env.API_INTERNAL_URL || "http://127.0.0.1:5050";
const config: NextConfig = {
  output: "standalone",
  async rewrites() {
    if (process.env.VERCEL) return [];
    return [
      { source: "/openapi/:path*", destination: `${backend}/openapi/:path*` },
      { source: "/api/:path*", destination: `${backend}/api/:path*` },
      { source: "/hubs/:path*", destination: `${backend}/hubs/:path*` },
    ];
  },
};
export default config;
