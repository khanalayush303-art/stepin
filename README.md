# StepIn

A job and internship recruitment platform connecting students and graduates with
verified employers.

> **Find opportunities. Apply with confidence. Build your future.**

The product journey is **Discover → Match → Prepare → Apply → Track → Interview →
Outcome**, and the interface is organised around it. It is deliberately not a
social feed: every surface is about roles, applications, profiles and
professional development.

---

## Phase status

This repository is at **Phase 1 — Authentication**.

| Delivered | Deliberately not here yet |
| --- | --- |
| Design system (Figma + code tokens), reusable component library | Profile / job / application CRUD |
| Public page shells, three dashboards | Resume uploads, matching, messaging, interviews |
| Backend skeleton: DI, config, EF Core, PostgreSQL, OpenAPI, CORS, problem details, structured logging, health probes | Any business entity or endpoint beyond the user record auth needs |
| **Real authentication via [Clerk](#authentication-clerk)** — sign-up/sign-in/Google/email verification/password reset, server-verified sessions, an application-level `Users` table keyed to the Clerk identity, and role-gated dashboards | Admin moderation logic, notifications |
| Docker Compose for web + api + database | — |
| Frontend e2e + accessibility tests, backend xUnit wiring + auth tests | — |

Every dashboard's *content* still renders from `apps/web/src/lib/placeholder-data.ts` —
only who's allowed to see it is real. That file is isolated on purpose and is
deleted, not migrated, when the underlying entities land.

---

## Design source of truth

**Figma — "Recruitment Platform — Design System"**
<https://www.figma.com/design/1reZHL0SnZoFuAPjwYA98E>

| Page | What it holds |
| --- | --- |
| Cover, Getting Started | Product framing and version metadata |
| Colour | 40 semantic tokens, Light + Dark, each aliased to a primitive ramp |
| Typography | Inter type ramp, Display → Caption/Overline/Button |
| Spacing & Layout | 4px scale, control heights, breakpoints, container |
| Radius & Elevation | Radius scale and the elevation hierarchy |
| Icons | 16-glyph stroke set (lucide equivalents) |
| Button, Form Controls, Badge & Status, Card, Navigation, Feedback & States, Domain Components | Component sets with variants and documented usage rules |
| Screens · Public / Auth / Dashboards | Home, Jobs, Sign in, Create account, Check your email, Applicant dashboard |

`apps/web/src/app/globals.css` mirrors that file token for token. **Changing a
colour, radius, type step or shadow means changing it in both places** — the CSS
file carries a header comment saying exactly that.

One token was changed during the accessibility audit and synced back to Figma:
`color/muted-foreground` moved from `slate/500` to `slate/600`, because
`slate/500` over `color/muted` measures 4.34:1 and fails WCAG 2.1 AA.

---

## Architecture

```
stepin/
├── apps/
│   ├── web/                        Next.js frontend
│   │   ├── src/app/
│   │   │   ├── (marketing)/        Home, Jobs, Internships, Companies, How it works, About
│   │   │   ├── (auth)/             Sign in / Register (Clerk), account setup
│   │   │   ├── (app)/              Applicant, Recruiter and Admin dashboards — role-gated layouts
│   │   │   └── globals.css         Design tokens — mirrors Figma
│   │   ├── src/components/
│   │   │   ├── ui/                 Primitives: button, input, badge, card, dialog, …
│   │   │   ├── layout/             Header, footer, page header, logo
│   │   │   ├── jobs/               Job card, company card, filters, status indicator
│   │   │   └── dashboard/          Dashboard shell, stat card
│   │   ├── src/lib/auth/           Clerk-appearance mapping, server-side role lookup, the /account-setup API call
│   │   ├── src/lib/                Types, utils, filter builders, Phase 0 fixtures
│   │   ├── src/proxy.ts            Clerk middleware (identity only — role checks live in each layout)
│   │   └── e2e/                    Playwright: pages, navigation, states, accessibility, auth
│   │
│   └── api/                        ASP.NET Core Web API (clean architecture)
│       ├── src/StepIn.Domain/          Entities and value objects. Zero dependencies. Includes ApplicationUser.
│       ├── src/StepIn.Application/     Use cases and abstractions. No EF Core.
│       ├── src/StepIn.Infrastructure/  EF Core, PostgreSQL, concrete services.
│       ├── src/StepIn.Api/             Host: pipeline, Clerk JWT validation, endpoints, OpenAPI, health.
│       └── tests/StepIn.Api.Tests/     xUnit wiring + auth tests over the real pipeline.
│
├── docker-compose.yml
└── .env.example
```

Dependencies point inward only: `Api → Infrastructure → Application → Domain`.
`Domain` references nothing, and `Application` never sees EF Core — the seam is
`IApplicationDbContext`, which exposes the unit of work and nothing else.

---

## Technology

**Frontend** — Next.js 16 (App Router), React 19, TypeScript (strict), Tailwind CSS 4,
Radix UI primitives, lucide-react, self-hosted Inter via `@fontsource-variable/inter`.

**Backend** — ASP.NET Core 10, C#, Entity Framework Core, Npgsql, Serilog,
built-in OpenAPI + Swagger UI.

**Database** — PostgreSQL 17.

**Infrastructure** — Docker, Docker Compose.

**Testing** — Playwright (+ axe-core) for the frontend, xUnit for the backend.

---

## Development setup

### Prerequisites

- Node.js 22+
- .NET SDK 10
- Docker Desktop (or Docker Engine + Compose v2)

### Environment

```bash
cp .env.example .env
```

`.env` is git-ignored. Every value in the example is a local-development default
and safe to share; nothing in this repository contains a real secret — including
the Clerk keys, which are a syntactically-valid placeholder, not a working
credential (see [Authentication](#authentication-clerk) below).

### Everything at once

```bash
docker compose up --build
```

| Service | URL |
| --- | --- |
| Web | <http://localhost:3000> |
| API | <http://localhost:5080> |
| Swagger UI | <http://localhost:5080/swagger> |
| OpenAPI document | <http://localhost:5080/openapi/v1.json> |
| Liveness | <http://localhost:5080/health/live> |
| Readiness (includes PostgreSQL) | <http://localhost:5080/health/ready> |
| PostgreSQL | `localhost:5432` (`stepin` / `stepin`) |

```bash
docker compose down          # stop
docker compose down -v       # stop and delete the database volume
docker compose logs -f api   # follow one service
```

### Frontend on its own

```bash
cd apps/web
npm install
npm run dev          # http://localhost:3000
npm run build
npm run start
npm run lint
npm run typecheck
```

### Backend on its own

Start just the database, then run the API from the SDK:

```bash
docker compose up -d db

cd apps/api
dotnet restore
dotnet build
dotnet run --project src/StepIn.Api    # http://localhost:5080
```

> **Package versions.** The `.csproj` files use floating versions
> (`10.0.*`, `9.*`). Run `dotnet restore`, then pin them to whatever resolved —
> floating versions are convenient for a first restore and a liability
> afterwards.

### Database migrations

One migration exists (`InitialCreate`): a single `stepin."Users"` table —
`Id`, `ClerkUserId` (unique), `Email` (unique), `FirstName`, `LastName`, `Role`
(nullable until account setup), `AccountStatus`, `CreatedAt`, `UpdatedAt`. To add
another migration once a later phase adds entities:

```bash
cd apps/api
dotnet tool install --global dotnet-ef       # once
dotnet ef migrations add SomeChange \
  --project src/StepIn.Infrastructure \
  --startup-project src/StepIn.Api
dotnet ef database update \
  --project src/StepIn.Infrastructure \
  --startup-project src/StepIn.Api
```

Migrations are applied automatically **in Development only**. In any other
environment applying them is a deploy step — two instances racing to migrate the
same database is not a failure mode worth inviting.

---

## Authentication (Clerk)

**Clerk is the only authentication provider.** It owns credentials, sessions,
Google sign-in, email verification and password reset — none of that is
implemented in this codebase. ASP.NET Core's job is authorization: it validates
the Clerk-issued token on every request, and is the source of truth for the
platform's own roles (`Applicant` / `Recruiter` / `Admin`), which Clerk has no
concept of.

```
Clerk (hosted)
   │  Authorization: Bearer <session token>
   ▼
Next.js  ──same-origin /api rewrite──▶  ASP.NET Core  ──▶  PostgreSQL
(ClerkProvider,                         (JWT Bearer against            (Users table:
 useAuth/useUser,                        Clerk's JWKS; no                ClerkUserId,
 auth.protect() per layout)              secret key needed)              Role, ...)
```

- **User sync is lazy, not webhook-based.** The first authenticated request for
  a new Clerk identity creates its `Users` row automatically
  (`ClerkUserSyncClaimsTransformation`); every later request reuses it. There is
  no `user.created` webhook to configure.
- **Route protection lives in each route group's `layout.tsx`**, not in
  middleware — Clerk deprecated middleware-based path matching
  (`createRouteMatcher`) in favor of per-page/layout checks, and this repo
  follows that guidance. `src/proxy.ts` only makes Clerk's auth state available;
  it doesn't decide anything.
- **Promoting an Admin** has no UI by design — do it directly once the person
  has signed up at least once:
  ```bash
  psql "$DATABASE_URL" -c "UPDATE stepin.\"Users\" SET \"Role\" = 'Admin' WHERE \"ClerkUserId\" = 'user_xxx';"
  ```
- **`WEB_ORIGIN` (→ `Clerk:AuthorizedParties`) must equal the real frontend
  origin wherever the API is deployed.** The API checks a token's `azp` claim
  against this allow-list and rejects anything else with 401 — a token from
  `https://your-app.vercel.app` is correctly refused if the API still only
  trusts `http://localhost:3000`.

### Local setup

1. Create a free application at <https://dashboard.clerk.com>.
2. Enable Google under **Configure → SSO connections** if you want it — this is
   entirely Clerk-side; nothing in this repo's code changes.
3. Copy three values into `.env`:
   ```env
   NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=pk_test_...
   CLERK_SECRET_KEY=sk_test_...
   CLERK_AUTHORITY=https://your-app.clerk.accounts.dev
   ```
   (`CLERK_AUTHORITY` is the same "Frontend API" domain Clerk shows you, used
   only for JWKS discovery — it is not a secret.)

### Troubleshooting

- **The app won't boot / throws about a missing publishable key.**
  `@clerk/nextjs` refuses to render at all without a *syntactically* valid key —
  `.env.example`'s placeholder satisfies the format check so the app boots, but
  it is not a real credential.
- **Sign-in/sign-up pages load, but nothing works, or Clerk's SDK logs a
  network error.** That's the placeholder key: it decodes to a Frontend API
  domain (`example.clerk.accounts.dev`) that doesn't correspond to a real
  Clerk application, so Clerk's own edge rejects it. Replace it with your real
  key from step 3 above.
- **A dashboard redirects back to `/sign-in` in a loop.** Almost always means
  the API is unreachable from the frontend — check `docker compose logs api`
  and that `Clerk:Authority` is set there too.

---

## Testing

### Frontend

```bash
cd apps/web
npm run test:e2e          # all three viewports
npm run test:e2e -- --project=desktop
npm run test:e2e:ui       # interactive
```

Playwright starts its own production server on port 3100. Five suites:

- `pages.spec.ts` — every public route returns 200, has exactly one `<h1>`, and does not scroll horizontally at any viewport.
- `navigation.spec.ts` — nav reaches each surface, `aria-current` marks the active route, the skip link is first in the tab order.
- `states.spec.ts` — loading, empty and error states; filtering and clearing.
- `accessibility.spec.ts` — axe-core against WCAG 2.1 A and AA on the public + sign-in/register routes.
- `auth.spec.ts` — `/dashboard`, `/recruiter`, `/admin` and `/account-setup` redirect an unauthenticated visitor to sign-in; the old forgot-password/reset-password/verify-email URLs redirect there too.

Projects run at 1440px (desktop), 834px (tablet) and a Pixel 7 profile (mobile).

**This suite needs a real Clerk key to fully pass** (see
[Authentication](#authentication-clerk)) — with only the placeholder key,
Clerk's client bootstrap makes a real request to its own edge for every page
(not just auth pages) and gets rejected, so every route 400s in a real browser
even though `next build`/`lint`/`typecheck` all stay green. This is inherent to
Clerk, not a bug here. Confirmed green (93 passed, 3 pre-existing skips, all
three viewports, including the WCAG scan against Clerk's real rendered
sign-in/sign-up) once a real key from your own Clerk application is in
`.env.local`.

The presentation states are reachable by hand too: `/jobs?state=loading`,
`?state=empty`, `?state=error`.

### Backend

```bash
cd apps/api
dotnet test
```

`HealthEndpointTests`/`ApiContractTests` cover the wiring: liveness stays
healthy when PostgreSQL is down, readiness returns 503 and names the failing
check, the OpenAPI document is served, CORS behaves, and an unknown route
returns `application/problem+json`. `AuthEndpointsTests` covers the real
Clerk-token-validation → user-sync → role-policy pipeline against a locally
signed token (shaped like Clerk's) and a real, ephemeral PostgreSQL container
via Testcontainers — **this half requires a running Docker daemon.**

---

## Accessibility

Target: **WCAG 2.1 AA**, verified in CI rather than asserted.

- axe-core reports **zero** WCAG 2.1 A/AA violations across 11 routes at three viewports.
- Status is never carried by colour alone — every badge pairs a tone with a dot and a text label, alerts change glyph per tone, and the application timeline carries glyph, fill weight and label.
- Focus is never removed: a 2px `color/ring` outline with 2px offset on every interactive element.
- Interactive controls are 44px tall by default, clearing the AA target-size minimum. The 36px `sm` size is for pointer-dense UI only.
- Every field has a persistent visible label; placeholders are examples, never labels. Errors are wired with `aria-invalid` and `aria-describedby` and announced via `role="alert"`.
- `prefers-reduced-motion` disables animation and smooth scrolling.
- A skip link is the first focusable element on every public page.

---

## Conventions

- **Tokens, not values.** No hard-coded colour, radius, shadow or type size in a component. If a value is missing, add the token to Figma and to `globals.css`.
- **Components, not copies.** A visual pattern that appears twice becomes a component in `src/components/`.
- **Server components by default.** `"use client"` only where interaction requires it.
- **Small page files.** Pages compose; they do not contain layout logic.
- **One `<h1>` per page**, headings never skip a level.

### One trap worth knowing about

`tailwind-merge` does not know about custom theme scales. Before it was
configured in `src/lib/utils.ts`, it classified `text-small` (a font size in our
ramp) as a *text colour*, so it silently dropped `text-primary-foreground` from
every button — slate text on an indigo fill, a 2.25:1 contrast failure that looks
plausible until you audit it. If you add a font size, radius, shadow or control
height to the theme, add it to the `extendTailwindMerge` config in the same
commit.

---

## Security

- No secrets in the repository. `.env` is git-ignored; `.env.example` holds local defaults only.
- No password ever touches this codebase — Clerk owns credentials entirely, and `CLERK_SECRET_KEY` never reaches the browser.
- The API trusts nothing a client sends about who it is; it only trusts the identity inside a Clerk token it has independently validated against Clerk's JWKS.
- CORS origins are configuration, never a wildcard.
- Error responses carry a trace id, not a stack trace, outside Development.
- The API container runs as a non-root user; the web container runs as `nextjs`.

---

## What Phase 2 picks up

The applicant profile (education, experience, projects, skills,
certifications), jobs, applications, and the business logic behind the
dashboards that currently render placeholder data. Authorization for all of it
builds on the role already established in this phase — `RequireApplicant` /
`RequireRecruiter` / `RequireAdmin` policies exist and are tested, just not yet
consumed by any endpoint.

The design system, component library, page shells, pipeline and container setup
should not need to change to accommodate any of it.
