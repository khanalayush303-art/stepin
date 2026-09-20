# StepIn — Architecture

This is the deeper technical companion to the README's architecture overview —
read that first for the directory tree and technology list. This document
covers *how a request actually flows* and *why the layers are shaped the way
they are*, since that's the part that doesn't fit in a tree diagram.

## System overview

```
Clerk (hosted)
   │  owns: credentials, sessions, Google sign-in, email verification, password reset
   │  issues: a short-lived Bearer session token
   ▼
Next.js (apps/web)
   │  ClerkProvider + clerkMiddleware make the token available to server code
   │  browser calls go to same-origin /api/* (next.config.ts rewrites), which
   │  Next.js's own server proxies to the API — no CORS needed for browser calls
   ▼
ASP.NET Core (apps/api)
   │  validates the Bearer token itself, against Clerk's public JWKS
   │  (Microsoft.AspNetCore.Authentication.JwtBearer — no Clerk SDK, no secret
   │  key needed on this side; JWKS is public)
   │  owns: authorization (roles), business logic, all application data
   ▼
PostgreSQL (via EF Core)
   owns: the Users table (keyed by ClerkUserId) and, from Phase 2 on,
   every other application entity
```

**Why the frontend never talks to Postgres or Clerk's admin APIs directly:**
Next.js is a *relay*, not a second backend. It knows how to ask Clerk "who is
this session" and how to attach that identity as a bearer token on calls to
the real API — it holds no authorization logic and makes no direct database
calls itself (`src/lib/auth/server.ts` is the one exception: it calls the
API's own `/me` endpoint server-side for layout-level route guards, it never
touches Postgres).

## Request lifecycle: an authenticated API call

1. Browser calls `fetch("/api/v1/auth/account-setup", { headers: { Authorization: \`Bearer ${token}\` } })` — `token` comes from Clerk's `useAuth().getToken()`.
2. Next.js's `rewrites()` config forwards `/api/v1/*` to the real API origin (`API_INTERNAL_BASE_URL` inside Docker, `NEXT_PUBLIC_API_BASE_URL` otherwise) — transparently, so the browser never learns the API's real address and never needs a CORS preflight for this call.
3. ASP.NET Core's JWT Bearer middleware validates the token's signature against Clerk's JWKS (fetched once, cached, keyed by `kid`), checks `iss` against `Clerk:Authority`, and checks the `azp` claim against `Clerk:AuthorizedParties` — a token minted for a frontend origin this API wasn't told to trust is rejected even if the signature is genuinely Clerk's.
4. `ClerkUserSyncClaimsTransformation` (an `IClaimsTransformation`, so it runs automatically right after authentication, before any `[Authorize]` policy is evaluated) reads the token's `sub` claim and finds-or-creates the matching `ApplicationUser` row in Postgres — idempotently, and racing-safe (see the file's own comment for the exact mechanism). It adds the user's internal id and current role as claims on the principal.
5. The endpoint handler (`AuthEndpoints.cs`) reads the caller's identity *only* from those claims — never from anything the client sent in the request body or query string. There is structurally no way to pass another user's id to `/me` or `/account-setup`, because neither endpoint accepts one.

## Layer responsibilities (`apps/api`)

| Project | Owns | Must never depend on |
| --- | --- | --- |
| `StepIn.Domain` | Entities (`ApplicationUser`), value objects, enums (`UserRole`, `AccountStatus`). Pure C#, no framework types. | Anything — this is the dependency graph's leaf. |
| `StepIn.Application` | Use-case-shaped abstractions (`IApplicationDbContext`, `IDateTimeProvider`). | EF Core, ASP.NET Core — the seam is deliberately narrow so use cases never see a `DbSet<T>` directly, only `IQueryable<T>`. |
| `StepIn.Infrastructure` | EF Core, Npgsql, `ApplicationDbContext`, entity configurations, migrations. | ASP.NET Core hosting concerns (no `Program.cs`-shaped code here). |
| `StepIn.Api` | Host: `Program.cs`, endpoint definitions, Clerk JWT Bearer setup, OpenAPI/Swagger, CORS, exception handling, rate limiting. | Nothing below it may reference this project — dependencies point inward only. |

## Why Clerk, and why this validation shape specifically

Clerk owns every credential-handling responsibility (password hashing,
session cookies/tokens, OAuth flows, email delivery for verification/reset).
The API's only job is to answer "is this token genuinely Clerk's, and who is
it for" — which is exactly what standard JWT Bearer + OIDC discovery already
solves, so no Clerk-specific SDK is needed server-side. This also means the
backend never holds a Clerk secret key at all: JWKS is a public endpoint by
design, and the `CLERK_SECRET_KEY` that *does* exist only lives in the
Next.js server environment (for `auth()`/`getToken()`), never the browser
bundle and never the API.

## Route protection shape (`apps/web`)

Clerk deprecated `clerkMiddleware()` + `createRouteMatcher()` path-matching in
favor of resource-based checks per page/layout (path matching can diverge
from how Next.js actually resolves a route). This repo follows that current
guidance: `src/proxy.ts` only makes Clerk's auth state available to server
code; the actual "must be signed in" / "must have this role" checks live in
each protected route group's own `layout.tsx` (`dashboard/`, `recruiter/`,
`admin/`, `account-setup/`), which calls `auth.protect()` and then asks the
API's `/me` for the role-specific redirect. This is UX convenience — the
API's own `[Authorize]`/role-policy checks are the real enforcement boundary,
and would refuse an unauthorized request even if a bug in the frontend layout
guard let it through.
