# AIDX lab permissions

AIDX contributors/editors/lab managers are a **separate permission system**
from StepIn's own platform roles (`Applicant`/`Recruiter`/`Admin`,
`StepIn.Domain.Common.UserRole`). A person can contribute to the lab without
ever becoming a platform `Admin`, and holding a platform role never implies
any AIDX permission.

## The roles

| Role | Can do |
| --- | --- |
| `Contributor` | Create and edit their own content; submit it for review. |
| `Editor` | Review content within their scope, request changes, publish, archive. Never their own submission. |
| `LabManager` | Manage lab memberships, research areas, researcher↔user links; everything an Editor can. |
| Platform `Admin` | Implicitly satisfies every AIDX check above — no `AidxMembership` row needed at all. This is "administrative oversight" as the original spec defines it, scoped **only** to the AIDX permission matrix; StepIn's own platform roles stay non-hierarchical as before. |

See `AidxPermissions` (`StepIn.Domain.Aidx`) for the one and only gate every
decision goes through — static, pure, no persistence access, mirroring
`AidxOwnershipRules`'s shape.

## Membership, not a claim

`AidxMembership` is **one row per user, ever** — role changes and revocation
mutate that same row (never create a new one), so every
`CreatedByMembershipId` ownership link and `AidxMembershipAuditEntry` always
points at a stable id. Revoking access sets `Status = Revoked`; it never
deletes the row, so authorship/ownership history stays intact.

Membership is **not** carried in the Clerk JWT as a claim. Every handler
re-resolves the caller's own membership row fresh from the database on every
request. This is deliberate: a claims-based policy would only refresh on
token renewal, reintroducing the exact staleness problem Clerk's own
role-claim withholding (`ClerkUserSyncClaimsTransformation`) already solves
for suspended accounts elsewhere in this codebase. The result: revoking
someone's membership takes effect on their very next request, not on their
next sign-in.

Every membership check additionally re-verifies
`ApplicationUser.AccountStatus == Active` at the point of use — defense in
depth alongside Clerk's own suspended-account enforcement, since
`AidxMembership` is resolved independently of the role claim that
enforcement withholds.

## Scoped editors

An `Editor` membership may carry a nullable `ScopedResearchAreaId`:

- `null` — lab-wide. Reviews anything, including `News`/`Event`, which have
  no research-area association at all.
- set — may act only on `Project`/`Publication` content tagged with that
  area. Has **zero** rights over `News`/`Event` regardless, since there's no
  area to match against.

Scope is re-checked fresh on every action against the content's **current**
tags — never cached from submission time — and is gated by the exact same
edit check as every other field, including the tags themselves: there is no
separate, more-permissive path for retagging content, so an actor can never
use a tag change to acquire rights they didn't already have.

## Bootstrap and recovery

The first `LabManager` is granted by an operator command, never a public
route:

```
dotnet StepIn.Api.dll aidx-bootstrap-manager --user-id <ApplicationUser Guid>
dotnet StepIn.Api.dll aidx-bootstrap-manager --clerk-user-id <user_...>
```

Email is never accepted as a selector —
`20260920111926_MakeEmailIndexNonUnique.cs` means it cannot be trusted to
resolve to exactly one account. The command refuses, with no write, if any
`Active` `LabManager` already exists (a one-time bootstrap, not a repeatable
grant) and wraps its check-and-insert in a transaction-scoped
`pg_advisory_xact_lock`, so two simultaneous invocations cannot both
succeed.

**Recovery**, if every `LabManager` is ever revoked: re-run the same
command. Its guard is "no currently `Active` `LabManager`," not "has never
run," so this is the stated, sole recovery path — performed by someone with
server/ops access (`fly ssh console` in production), never exposed as an
HTTP endpoint. There is no other privilege-escalation route.

## Ownership vs. attribution

- **Attribution** — who's credited: `AidxPublicationAuthor.ResearcherId` /
  `AidxNews.AuthorResearcherId`, pointing at an `AidxResearcher`, which may
  have no `UserId` at all (most researchers have no StepIn account).
- **Ownership** — who may edit: `CreatedByMembershipId`, pointing at a real
  `AidxMembership`.

No code path ever checks an author field for authorization. Appearing as an
author never grants any permission — this is enforced by construction, not
convention: being listed as a paper's co-author never makes someone able to
edit or publish it.

A legacy row created before this system existed has `CreatedByMembershipId
== null`. It is **never** implicitly open to any Contributor's edit — only
an Editor (within scope)/LabManager/Admin can explicitly claim it via a
`claim-ownership` action (Milestone 2), which is itself audited like every
other editorial action.
