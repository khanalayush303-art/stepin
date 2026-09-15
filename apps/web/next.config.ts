import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Produces .next/standalone, which the Dockerfile copies instead of the full
  // node_modules tree.
  output: "standalone",

  // Surfaces the API base URL to the browser. Phase 1 starts calling it; Phase 0
  // only needs the wiring to exist so the value is not hard-coded later.
  env: {
    NEXT_PUBLIC_API_BASE_URL: process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080",
  },

  // The browser only ever talks to the API through this same-origin proxy —
  // never NEXT_PUBLIC_API_BASE_URL directly. That's what lets the Identity
  // auth cookie be HttpOnly + SameSite=Lax instead of the cross-site
  // SameSite=None (Secure, HTTPS-only) a separate-origin cookie would need,
  // and it's why there's no localStorage token anywhere in this app.
  async rewrites() {
    const apiOrigin = process.env.API_INTERNAL_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";
    return [{ source: "/api/:path*", destination: `${apiOrigin}/api/:path*` }];
  },

  async headers() {
    return [
      {
        source: "/:path*",
        headers: [
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
          { key: "X-Frame-Options", value: "DENY" },
        ],
      },
    ];
  },
};

export default nextConfig;
