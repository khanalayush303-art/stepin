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

This repository is at **Phase 0 — Foundation**.

| Delivered in Phase 0 | Deliberately not here yet |
| --- | --- |
| Design system (Figma + code tokens) | Real authentication, Google OAuth, password reset logic |
| Reusable component library | Profile / job / application CRUD |
| Public page shells, auth screens, three dashboards | Resume uploads, matching, messaging, interviews |
| Backend skeleton: DI, config, EF Core, PostgreSQL, OpenAPI, CORS, problem details, structured logging, health probes | Any business entity or endpoint |
| Docker Compose for web + api + database | Admin moderation logic, notifications |
| Frontend e2e + accessibility tests, backend xUnit wiring tests | — |

Every screen you can click today renders from `apps/web/src/lib/placeholder-data.ts`.
That file is isolated on purpose and is deleted, not migrated, when the API lands.

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
│   │   │   ├── (auth)/             Sign in, Register, Forgot/Reset password, Verify, Setup
│   │   │   ├── (app)/              Applicant, Recruiter and Admin dashboards
│   │   │   └── globals.css         Design tokens — mirrors Figma
│   │   ├── src/components/
│   │   │   ├── ui/                 Primitives: button, input, badge, card, dialog, …
│   │   │   ├── layout/             Header, footer, page header, logo
│   │   │   ├── jobs/               Job card, company card, filters, status indicator
│   │   │   ├── dashboard/          Dashboard shell, stat card
│   │   │   └── auth/               Google button, auth divider
│   │   ├── src/lib/                Types, utils, filter builders, Phase 0 fixtures
│   │   └── e2e/                    Playwright: pages, navigation, states, accessibility
│   │
│   └── api/                        ASP.NET Core Web API (clean architecture)
│       ├── src/StepIn.Domain/          Entities and value objects. Zero dependencies.
│       ├── src/StepIn.Application/     Use cases and abstractions. No EF Core.
│       ├── src/StepIn.Infrastructure/  EF Core, PostgreSQL, concrete services.
│       ├── src/StepIn.Api/             Host: pipeline, endpoints, OpenAPI, health.
│       └── tests/StepIn.Api.Tests/     xUnit wiring tests over the real pipeline.
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
and safe to share; nothing in this repository contains a real secret.

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

No migrations exist yet, because Phase 0 has no entities. When the first entity
lands:

```bash
cd apps/api
dotnet tool install --global dotnet-ef       # once
dotnet ef migrations add InitialCreate \
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

## Testing

### Frontend

```bash
cd apps/web
npm run test:e2e          # all three viewports
npm run test:e2e -- --project=desktop
npm run test:e2e:ui       # interactive
```

Playwright starts its own production server on port 3100. Four suites:

- `pages.spec.ts` — every route returns 200, has exactly one `<h1>`, and does not scroll horizontally at any viewport.
- `navigation.spec.ts` — nav reaches each surface, `aria-current` marks the active route, the skip link is first in the tab order.
- `states.spec.ts` — loading, empty and error states; filtering and clearing; form validation announced in text.
- `accessibility.spec.ts` — axe-core against WCAG 2.1 A and AA on 11 routes.

Projects run at 1440px (desktop), 834px (tablet) and a Pixel 7 profile (mobile).

The presentation states are reachable by hand too: `/jobs?state=loading`,
`?state=empty`, `?state=error`.

### Backend

```bash
cd apps/api
dotnet test
```

Covers the wiring Phase 0 is actually about: liveness stays healthy when
PostgreSQL is down, readiness returns 503 and names the failing check, the
OpenAPI document is served and contains the expected paths, CORS allows the
configured origin and refuses an unknown one, and an unknown route returns
`application/problem+json` rather than an HTML error page.

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
- CORS origins are configuration, never a wildcard.
- Error responses carry a trace id, not a stack trace, outside Development.
- The API container runs as a non-root user; the web container runs as `nextjs`.

---

## What Phase 1 picks up

Authentication and identity: ASP.NET Core Identity, the credential and Google
OAuth flows behind the screens that already exist, email verification and
password reset, and the first real entities with their migrations.

The design system, component library, page shells, pipeline and container setup
should not need to change to accommodate any of it.
