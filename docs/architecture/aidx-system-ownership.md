# AIDX system ownership

AIDX Research Jobs reuse the existing StepIn `Job` model. That model requires a
`RecruiterProfile` and a `Company`, so AIDX needs a backend-owned recruiter identity to hold
them. This page explains that identity and the rules around it.

## Why the system identity exists

A `Job` cannot exist without a `RecruiterProfileId` and a `CompanyId`. Rather than add a second
job-ownership system, AIDX Research Jobs are ordinary `Job` rows with `Category = Research`,
owned by one dedicated recruiter profile at one dedicated company.

## Why it is not a Clerk user

The system identity is an ownership record. It is not a person and must never sign in.

- It has no Clerk account and no Clerk user ID. Clerk IDs start with `user_`. The system subject
  is `system:aidx-lab`, which Clerk cannot issue.
- Its account status is `Suspended`, and its platform role is unset, so no recruiter, applicant, or
  admin policy can pass.
- The sign-in sync (`ClerkUserSyncClaimsTransformation`) never turns a `system:` subject into an
  app-user or role claim. A token carrying that subject gets 401.

## Ownership flow

```text
AIDX Admin (normal Clerk account)
    │  RequireAdmin
    ▼
AIDX opportunity API            (Phase 4.4C)
    │  server-controlled ownership
    ▼
AIDX system RecruiterProfile    (UserId = system user, CompanyId = AIDX Lab)
    │
    ▼
AIDX Lab Company
    │
    ▼
Research Job                    (Category = Research)
```

The ownership chain is:

```text
ApplicationUser (system:aidx-lab, Suspended, no role)
      ↓
RecruiterProfile (backend-only)
      ↓
Company ("AIDX Lab")
      ↓
Job (Category = Research)
```

## Important rule

```text
AIDX Admin ≠ AIDX system user
```

An admin authenticates with their own Clerk account, under `RequireAdmin`. The system user exists
only so that Research Jobs have an owner.

## Code map

| Concern | Location |
| --- | --- |
| Canonical identifiers, reserved subject check | `StepIn.Domain/Aidx/AidxSystemIdentity.cs` |
| Ownership record and the Research-only rule | `StepIn.Domain/Aidx/AidxSystemOwnership.cs` (`AidxOwnershipRules`) |
| Idempotent initialisation and read-only lookup | `StepIn.Infrastructure/Aidx/AidxSystemOwnershipService.cs` |
| Sign-in guard for the reserved subject | `StepIn.Api/Infrastructure/ClerkUserSyncClaimsTransformation.cs` |
| Tests | `StepIn.Api.Tests/AidxSystemOwnershipTests.cs` |
| Admin opportunity API | `StepIn.Api/Endpoints/AidxOpportunityAdminEndpoints.cs`, `AidxOpportunityAdminContracts.cs` |
| Opportunity API tests | `StepIn.Api.Tests/AidxOpportunityAdminTests.cs` |

## Admin opportunity API (`/api/v1/admin/aidx/opportunities`)

This route manages AIDX-owned Research Jobs only. It is not a general Job administration API.

- **Boundary:** every operation applies `AidxOwnershipRules.OwnedByAidx`. That predicate requires
  `Category == Research`, the system recruiter profile, and the AIDX Lab company. A Career job, another
  recruiter's Research job, or a job at another company returns 404, the same as an unknown ID.
- **Authorization:** `RequireAdmin` on every endpoint. Anonymous gets 401. Applicant and Recruiter get 403.
- **Ownership is server-side.** The request DTO has no owner, company, category, or status field, and
  unknown JSON properties are ignored. Create always starts as a draft.
- **Owner unavailable:** if the system owner is missing or inconsistent, every route returns 503 with a
  generic message. Nothing is created. Initialisation is a separate, deliberate step.

### Lifecycle

```text
POST    → Draft
Publish → Published        (from Draft or Unpublished; project must be Published if linked)
Unpublish → Unpublished    (from Published only; project link is kept)
DELETE  → removed          (Draft only)
```

Lifecycle rule violations return a validation problem, the same convention as the recruiter endpoints.

### Project linking

- No project: publish allowed.
- Published project: publish allowed.
- Draft or archived project: publish rejected.
- A published opportunity cannot be relinked to a non-published project.
- Publishing or unpublishing never changes the project's status.
- If a linked project is later unpublished, the opportunity stays `Published` internally and is hidden
  from public listings by the existing visibility rule.

### Application

No AIDX application endpoint exists. Candidates apply through the existing
`POST /api/v1/jobs/{jobId}/applications`, which accepts any published job, including AIDX research jobs.

## Initialisation

`AidxSystemOwnershipService.EnsureAsync` creates the system user, the AIDX Lab company, and the
system recruiter profile, and returns their IDs. It is idempotent: a second call finds the existing
records and creates nothing.

- **The canonical company is the one the system profile points to, never one found by name.** A
  real recruiter may already have a company called "AIDX Lab", and it must not be adopted.
- **Inconsistent state throws.** Examples include a role on the system user, the profile pointing at a
  different company, or a renamed company. The service does not repair these, because silent repair could
  mis-attribute opportunities.
- **It is not run automatically.** Nothing calls it at startup, so production data is never written as a
  side effect of deploying.
- **The operator command is the only entry point:** `dotnet StepIn.Api.dll aidx-init-owner`
  (`apps/api/src/StepIn.Api/Operations/AidxOwnerInitCommand.cs`). It is not an HTTP route. It checks with the
  read-only `FindAsync` first, so a consistent owner is reported with no write. It exits 2 with
  "No changes were made" for inconsistent state. It never starts the web host or applies migrations. The
  production procedure is in [docs/deployment/production-release.md](../deployment/production-release.md).
- **Validation runs before any write** for an existing user. A user holding a role is refused before a
  company or profile is created.

## Rules for future AIDX endpoints (Phase 4.4C)

- Ownership is always resolved server-side with `EnsureAsync` or `FindAsync`. Requests never supply
  `RecruiterProfileId`, `CompanyId`, `UserId`, or `ClerkUserId`.
- Every AIDX opportunity query and mutation must include the Research check
  (`AidxOwnershipRules.IsAidxResearchJob`). A Career job's ID must return 404.
- Admin access uses the existing `RequireAdmin` policy. No new policy or role is introduced.
- Recruiter job endpoints are unchanged. A normal recruiter never gains access to AIDX Research Jobs.
