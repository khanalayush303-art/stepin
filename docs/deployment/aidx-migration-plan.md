# AIDX editorial-workflow migration plan

Two migrations so far:

1. `20261014145029_AddAidxMembership` — purely additive (new
   `AidxMemberships`/`AidxMembershipAuditEntries` tables, nullable
   `CreatedByMembershipId` columns on existing AIDX content tables).
2. `20261014153949_AddAidxEditorialWorkflow` — the one this document is
   about. Widens `AidxContentStatus`, adds `RevisionOfId`/`RevisionNumber`/
   `MergedAt`/`DiscardedAt`/`xmin` to every workflow content table, adds
   `AidxPublication.Status` (new) alongside its existing `Published` bool
   (kept), and creates `AidxContentSnapshots`/`AidxEditorialAuditEntries`.

## Expand, not contract

`AidxPublication.Published` (the pre-existing bool) is **not dropped**.
`Status` is the new source of truth, backfilled once
(`true→Published`/`false→Draft`) in this migration's `Up()`. Every
application write path that changes `Status` also writes the equivalent
`Published` bool for the duration of the rollout window, so an old,
not-yet-redeployed API instance reading `Published` still sees a correct
value.

Dropping `Published` is a **separate, later, explicitly out-of-scope**
contract migration — never bundled with this expand step, and not part of
this delivery at all.

## What "safe to revert" actually means here — not more than this

- **The migration's own `Down()` is safe only before any row exists in the
  new workflow shape.** Before go-live, nothing has ever been
  `Submitted`/`UnderReview`/a revision — reverting the schema at that point
  discards nothing real.
- **Once real data exists in the new shape, running `Down()` is itself a
  destructive operation** — it drops the columns/tables holding that
  data. It is explicitly **not** the rollback mechanism after go-live.
- **The actual rollback after go-live is reverting application code**,
  leaving the expand-migration schema in place — which is exactly why the
  legacy `Published` bool keeps receiving dual-writes: old code can still
  correctly read `Published` after a code-only rollback, even though it has
  no idea what `Submitted`/`UnderReview`/`ChangesRequested` mean.
- **Old code must not be allowed to write while new code is also live.**
  Old code flipping `Published = true` directly (e.g. via the pre-existing
  Admin UI, if somehow still deployed) could prematurely publish something
  genuinely mid-review under the new system. The rollout runbook requires a
  strict single-writer cutover — old code fully drained before new code
  takes writes, and vice versa on a rollback — not a novel mechanism, just
  an explicit, stated requirement of this plan.
- **No claim of "zero data loss" is made without an actual rehearsal.** A
  migration/rollback rehearsal against a staging-equivalent dataset is a
  required pre-production step; this document states the intended shape of
  that rehearsal, it does not substitute for having run one.

## Required configuration (new, not yet set anywhere)

Reported by name only — no value is ever printed here or anywhere else:

- `Storage:Supabase:AidxMediaBucket` — a separate bucket name from the
  existing resume bucket. Reuses the already-configured
  `Storage:Supabase:Url`/`:ServiceKey` secret values; nothing new to
  provision there, just this one new bucket and its name in config.

## Production rollout sequence (not yet executed — local only so far)

1. Apply `migrate-database` naming `20261014153949_AddAidxEditorialWorkflow`
   as the target (includes the membership migration before it in the same
   invocation if not already applied).
2. Confirm via read-only query that `AidxPublications.Status` is populated
   for every row and matches `Published` (`Status='Published' <=> Published=true`).
3. Deploy the new API image.
4. Run `aidx-bootstrap-manager` once, for the intended first LabManager.
5. Monitor for `DbUpdateConcurrencyException`-mapped `409`s at a sane rate
   (expected occasionally under real concurrent editing; a sustained spike
   would indicate a client-side version-tracking bug, not a backend issue).
