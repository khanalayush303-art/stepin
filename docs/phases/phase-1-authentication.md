# Phase 1 — Authentication

**Status: complete**, with one documented limitation (below). See
[../architecture/architecture.md](../architecture/architecture.md) for how the
pieces described here fit together, and the README's
[Authentication (Clerk)](../../README.md#authentication-clerk) section for
day-to-day setup instructions.

## What this phase delivered

- Real authentication via **Clerk** — sign-up, sign-in, Google sign-in, email
  verification and password reset, all Clerk's own hosted flows. Nothing in
  this codebase stores a password or issues its own session.
- An application-level `Users` table in PostgreSQL, keyed to the Clerk
  identity via `ClerkUserId` (unique), synced lazily and idempotently on a
  user's first authenticated request — no webhook to configure.
- Three platform roles (`Applicant` / `Recruiter` / `Admin`), assigned via a
  one-time account-setup step after sign-up, enforced server-side through
  ASP.NET Core authorization policies backed by the synced role — never by
  anything the client claims about itself.
- Role-gated dashboards (`/dashboard`, `/recruiter`, `/admin`) and a protected
  `/account-setup` step, each guarded by its own route-group layout.
- A full backend test suite exercising the real Clerk-token-validation →
  user-sync → role-policy pipeline (`AuthEndpointsTests`, against a real
  ephemeral PostgreSQL container via Testcontainers) and a full frontend e2e
  suite (Playwright, 3 viewports, WCAG 2.1 A/AA scan included) against Clerk's
  actual rendered sign-in/sign-up components.

## What was deliberately not built

Per the phase boundary: no applicant profile, jobs, applications, documents,
recruiter/admin business features, matching, messaging, or notifications.
Dashboards render from `apps/web/src/lib/placeholder-data.ts`, unchanged from
Phase 0 — only *who is allowed to see them* is real. The `RequireApplicant` /
`RequireRecruiter` / `RequireAdmin` authorization policies exist and are
tested, but nothing in this phase consumes them yet; Phase 2 is expected to.

## Verification performed

| Check | Result |
| --- | --- |
| Backend build (`dotnet build`) | Pass |
| Backend wiring tests (`HealthEndpointTests`, `ApiContractTests`) | Pass (7/7) |
| Backend auth tests (`AuthEndpointsTests`, Testcontainers) | **Blocked** — no Docker daemon available in the environment this phase was verified in |
| JWT validation against a **real** Clerk instance's JWKS | Pass — confirmed live; a forged token's rejection log shows the real key id fetched from Clerk, proving genuine JWKS discovery, not just local-key validation |
| Frontend build, `tsc --noEmit`, `eslint` | Pass |
| Playwright e2e, all 3 viewports, against a real Clerk key | Pass — 93 passed, 3 pre-existing/unrelated skips, 0 failed |
| WCAG 2.1 A/AA scan on Clerk's real rendered sign-in/sign-up | Pass |
| Vercel production deployment (`stepin-psi.vercel.app`) | Pass — home/sign-in/register 200, protected routes correctly redirect, no secret found in served HTML or the client bundle |
| Secrets audit (git history, tracked files, client bundle) | Pass — no real credentials committed anywhere; `.env`/`.env.local` correctly git-ignored |

## Known limitations

1. **No backend is deployed anywhere.** The Vercel deployment is frontend
   only; `NEXT_PUBLIC_API_BASE_URL` there still points at a placeholder, so
   sign-up/sign-in render correctly on the live site but account-setup (which
   needs the real API) cannot complete until a backend host exists.
2. **Local Docker was unavailable throughout this phase's verification**, so
   the persistence-backed backend test suite has never actually been *run*
   locally — only reviewed and reasoned through. The tests are written and
   ready; `cd apps/api && dotnet test` will run them the moment Docker is
   available.
3. Vercel's environment variables still carry unused vars left over from a
   pre-Clerk experiment (`SMTP_*`, `GOOGLE_CLIENT_*`, `DEV_ADMIN_*`,
   `POSTGRES_*`) — harmless (nothing reads them) but worth deleting during a
   future cleanup pass.

## A note on the migration this phase includes

Phase 1 was originally built on ASP.NET Core Identity (cookie sessions,
`UserManager`/`SignInManager`, custom SMTP-based email verification and
password reset, in-house Google OAuth) before being migrated to Clerk mid-phase.
That migration is why the git history contains a dedicated
`Migrate authentication from custom Identity/SMTP to Clerk` commit — nothing
from the Identity-based implementation remains; it was fully removed, not
layered underneath Clerk.
