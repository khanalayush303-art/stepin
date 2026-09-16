import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Produces .next/standalone, which the Dockerfile copies instead of the full
  // node_modules tree.
  output: "standalone",

  // Surfaces the API base URL to the browser. Phase 1 starts calling it; Phase 0
  // only needs the wiring to exist so the value is not hard-coded later.
  env: {
    NEXT_PUBLIC_API_BASE_URL: process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080",
    // Fixed route names, not per-environment config — no reason to make every
    // deployment set these via .env.
    NEXT_PUBLIC_CLERK_SIGN_IN_URL: "/sign-in",
    NEXT_PUBLIC_CLERK_SIGN_UP_URL: "/register",
    NEXT_PUBLIC_CLERK_SIGN_IN_FALLBACK_REDIRECT_URL: "/account-setup",
    NEXT_PUBLIC_CLERK_SIGN_UP_FALLBACK_REDIRECT_URL: "/account-setup",
  },

  // The browser only ever talks to the API through this same-origin proxy —
  // never NEXT_PUBLIC_API_BASE_URL directly. Auth itself is Clerk's (a Bearer
  // token attached per request, not a cookie), but this still avoids having
  // to configure CORS for the frontend origin at all.
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
