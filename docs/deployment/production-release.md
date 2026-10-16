# Production release runbook (StepIn + AIDX Lab)

Status: **pre-production.** Nothing in this document has been run against production. Each step is
an operator action, in the order below.

## Release readiness snapshot (2026-10-17, read-only production inspection)

Everything below was read directly from production (`fly status`/`releases`/`image show`/`secrets
list` — names only, never values — `fly postgres connect` for read-only `SELECT`s, and `fly logs`
for the already-logged non-secret "Resume storage provider" line) or from a local Docker rebuild
of the exact current commit. Nothing was changed.

- **Deployed commit:** `feb12cc` (API image label `org.opencontainers.image.revision`), release
  v19, both machines currently **stopped** (idle scale-to-zero, `fly.toml`'s
  `min_machines_running = 0` — not an incident). Local `HEAD` is **12 commits ahead**, none pushed.
- **Applied migrations (11):** up through `20261012131143_AddExperiencePassport`.
- **Pending migrations (6, all additive — every `DropColumn`/`DropTable` in these files is in
  `Down()`, never `Up()`):** `AddAidxMembership`, `AddAidxEditorialWorkflow`, `AddAidxCollaboration`,
  `AddAidxResearcherImageAltText`, `AddAidxNewsEventImageAltText`, `AddAidxProjectImage`.
- **Data risk is low:** `aidx."AidxProjects"`, `AidxResearchers`, `AidxPublications`, `AidxNews`,
  `AidxEvents`, `AidxResearchAreas` are all **empty** in production. The `AddAidxEditorialWorkflow`
  backfill (synthetic snapshot #1 for every currently-`Published` row) therefore affects **zero
  rows** this release. `stepin."Users"` = 7, `"Jobs"` = 1, `"JobApplications"` = 0.
- **No production Admin exists** (`stepin."Users"` has 0 rows with `"Role" = 'Admin'`) — unchanged
  from the prior release's note. The Experience Passport verification queue still has no reviewer.
- **No AIDX membership exists yet** (`aidx."AidxMemberships"` doesn't exist until migrated) —
  `aidx-bootstrap-manager` has never been run. **The first LabManager must be named by the release
  operator** — this runbook does not and should not guess who, since it requires picking a specific
  person's account.
- **Release-blocking config gap, found this pass:** production has `Storage__Provider=Supabase`
  (confirmed via the non-secret "Resume storage provider: Supabase" log line) but **no
  `Storage__Supabase__AidxMediaBucket` secret**. `AddAidxMediaStorage` (added this phase) throws at
  startup if `Storage:Provider=Supabase` and any of `Url`/`ServiceKey`/`AidxMediaBucket` is unset —
  deploying the new image as-is would fail to reach a healthy machine. A new **private** Supabase
  bucket for AIDX media must be created and its name set via `Storage__Supabase__AidxMediaBucket`
  **before** the API deploy. (`Storage__Supabase__Url`/`ServiceKey` are reused as-is, nothing new to
  provision there — see `docs/deployment/aidx-migration-plan.md`.)
- **Local build/test verification, this commit:** `dotnet build`/`dotnet test` 547/547 (real
  Testcontainers Postgres, not mocked), `npm run typecheck`/`lint`/`build` all clean, a from-scratch
  `docker compose build --no-cache` of both `api` and `web` succeeded (the API's `-c Release`
  publish matches the production Dockerfile path exactly), and the rebuilt containers were brought
  up locally: `/health/live`/`/health/ready` both 200, the one migration pending in the local dev DB
  applied cleanly on startup, `/api/v1/jobs` and `/api/v1/aidx/projects` return 200, and
  membership-gated/admin routes correctly return 401 without a token. This exercises the same
  author→submit→review→publish code paths the full test suite already covers end to end (see the
  new `AidxContentAuthoringEndpointsTests` lifecycle test), but is not a substitute for the
  production smoke tests below, which need a real Clerk session per role.
- **Frontend/backend currently live and healthy:** `https://stepin-psi.vercel.app/` and
  `https://stepin-api.fly.dev/health/live`+`/health/ready` all returned 200 as of this check — this
  is the pre-release baseline to compare against after the release.
- **Pushing `master` auto-deploys the Vercel project** (`ak-afe1/stepin`, confirmed via `vercel
  project ls`). The rollout order below accounts for this explicitly: the API is deployed and
  verified healthy with the new routes/migrations **before** `master` is pushed, so the moment the
  frontend goes live it is talking to a backend that already understands it — never the other
  direction.

## Exact rollout sequence for this release (awaiting authorization — nothing below has run)

In order, each gated on the previous step succeeding. "Operator" = whoever runs this with
production Fly/Vercel/GitHub access; none of these commands have been run by this session.

1. **Backup.** Confirm the newest `pg_data` volume snapshot is recent (one exists from within the
   last 24h via Fly's automatic daily snapshot, 5-day retention), or take one explicitly right
   before migrating: `fly volumes snapshots create <pg_data volume id> -a stepin-api-db`.
2. **AIDX media storage.** Create a new **private** Supabase Storage bucket for AIDX media (do not
   reuse the resume bucket). Set its name: `fly secrets set Storage__Supabase__AidxMediaBucket=<bucket> -a stepin-api`.
   (This alone restarts the *currently running old* API with the new secret present — harmless,
   since old code never reads that key — but it is a production change the operator should expect.)
3. **Migrate.** `fly ssh console -a stepin-api -C "dotnet StepIn.Api.dll migrate-database 20261015114655_AddAidxProjectImage"`
   — applies all 6 pending migrations in one invocation, naming the last one as the explicit safety
   gate (see §2 below for the command's full behavior/exit codes). Verify via the read-only query in
   §2.
4. **Deploy the API.** `fly deploy --build-arg GIT_SHA=$(git rev-parse HEAD) -a stepin-api`. This
   builds and deploys directly from the local checkout — it does **not** require pushing to GitHub
   first. Verify `/health/live` and `/health/ready`.
5. **Name the first LabManager.** Only after the operator has decided *who* — `fly ssh console -a stepin-api -C "dotnet StepIn.Api.dll aidx-bootstrap-manager --user-id <Guid>"` (or `--clerk-user-id`). See §4.
6. **Push `master`.** Only now — this is what triggers the Vercel production deploy. Pushing earlier
   would put a frontend expecting the new API routes (authoring GET, `/audit`, project images,
   membership search, `RevisionOfId` fields) in front of the old, not-yet-deployed API. Promote the
   resulting Vercel deployment to production once its own preview build passes.
7. **Smoke-test and monitor.** Run the checklist in "Smoke tests" below, including the new AIDX
   authoring/review/publish flow, then watch `fly logs -a stepin-api` and Vercel logs per
   "Monitoring" below.

Rollback for each numbered step is the corresponding entry in "Rollback" below — nothing here
introduces a new rollback path beyond what's already documented.

## Topology (as observed)

| Part | Where | Notes |
|---|---|---|
| Frontend | Vercel project `ak-afe1/stepin` | Next.js. Proxies `/api/*` to the API origin (`API_INTERNAL_BASE_URL`, falling back to `NEXT_PUBLIC_API_BASE_URL`). |
| API | Fly app `stepin-api` (region `syd`) | ASP.NET Core. Health check `/health/live`. Config in `apps/api/fly.toml`. |
| Database | Fly app `stepin-api-db` | `flyio/postgres-flex`. Volume `pg_data`, encrypted, 1 GB. Volume snapshots retained 5 days. |
| Resumes | Fly volume `stepin_resumes` (to be created) mounted at `/app/data` | See "Resume storage" below. |
| Auth | Clerk | The API validates tokens against `Clerk__Authority`, with an `azp` allow-list. |

## Compatibility

The AIDX migration (`20261005021348_AddAidxFoundations`) is additive. The new API reads
`Jobs.Category` and `Jobs.AidxProjectId`, so the schema must be migrated **before** the new API serves
traffic.

- **Old API + migrated schema:** works. The old code never selects the new columns, and the new
  `Category` column has a default, so existing rows and inserts stay valid.
- **New API + old schema:** does **not** work. Job queries select `Category`, so every job query fails.
- **Therefore:** migrate first, then release the API. Do not release the API first.

## Current state (recorded in Phase 4.4G.2)

| Item | State |
|---|---|
| Pre-migration database snapshot | `vs_yeMoe3m3Z9ewH1NvYg33gDlj` on `pg_data` (`vol_vp2j282l2odqnnj4`), status `created`, 5-day retention. |
| Restore from snapshot | **Not tested.** The procedure is documented, not verified. |
| Resume volume | `stepin_resumes` (`vol_r1jw2okgxqqpp5wr`) created in `syd`, 1 GB, encrypted. **Not attached**: no machine has it until a deploy with the `[[mounts]]` block. |
| `Storage__ResumeRootPath` | Staged with `--stage`. The stored value's digest matches the value already present, so it was `/app/data/resumes` before this phase. Runtime use is **not verified** until the app runs with the volume mounted. |
| Production migration state | **Unknown.** Could not be read without production credentials. The check is in step 3 below. |
| Migration rehearsal | Done on a disposable local PostgreSQL 17 (not production data). See below. |
| Production AIDX owner | **Not created.** |

**Migration rehearsal (local, disposable database, synthetic data):** the database was migrated to
`20261001214802_AddSavedJobs`, then one Career job, its user, company and recruiter profile were
seeded. Then `20261005021348_AddAidxFoundations` was applied. Results: `aidx` schema present with 12
tables; `Jobs.Category` is `NOT NULL`, default `'Career'`; `Jobs.AidxProjectId` is nullable; the seeded
Career job kept `Category = Career`, `AidxProjectId = NULL`, `Status = Published`; row counts unchanged.
Seven migrations are recorded in `stepin.__ef_migrations_history`. This does not replace a rehearsal on a
restored copy of production data, which is still required.

## 1. Pre-deployment checks

1. Record the commit to release, and confirm the working tree is clean.
2. Confirm a recent database snapshot exists:
   `fly volumes snapshots list <pg_data volume id> -a stepin-api-db`. The newest should be from
   just before the release. If it isn't, take one manually (the operator does this; it is not
   part of this repository's tooling).
3. Confirm the current production schema before migrating:
   `fly proxy 15432:5432 -a stepin-api-db`, then from a second terminal list
   `dotnet ef migrations list` with the production connection supplied as an environment variable.
   Read the migration history: `SELECT "MigrationId" FROM stepin.__ef_migrations_history ORDER BY 1;` (EF names this table in lowercase, in the `stepin` schema). **Do not paste the connection string into a file or the repo.**
4. Confirm the run-time configuration (names only): `ConnectionStrings__Postgres`, `Clerk__Authority`,
   `Clerk__AuthorizedParties__0`, `Cors__AllowedOrigins__0`, `ASPNETCORE_ENVIRONMENT`, and
   `Storage__ResumeRootPath`.

## 2. Apply the migration

Migrations are **not** applied at API startup outside Development (`Program.cs`). This step is the only
way production gets a new schema.

### Preferred: the `migrate-database` operator command (added 2026-10-13)

`StepIn.Api.Operations.MigrateDatabaseCommand` applies pending migrations using the application's
own, already-deployed `ConnectionStrings__Postgres` — the exact configuration the running API reads,
with no connection string ever typed, copied, or pasted by the operator. It runs the same way
`aidx-init-owner` does (see §4 below): inside the already-deployed container, over `fly ssh console`,
so the real secret never leaves the machine and never passes through an operator's own shell history
or this repository.

```bash
fly ssh console -a stepin-api -C "dotnet StepIn.Api.dll migrate-database <expected-final-migration-id>"
```

`<expected-final-migration-id>` is **mandatory** and is the actual safety gate, not a formality: name
the last migration you reviewed and intend to apply this run (for this release,
`20261012131143_AddExperiencePassport`). The command:

1. Connects using the already-configured connection string; reports and exits (code 2) if it can't,
   touching nothing.
2. Reads the migrations compiled into the running image and the database's own applied-migration
   history; refuses (code 3) if the history contains a migration this image doesn't recognise — a
   newer or divergent schema than expected — without applying anything.
3. Computes the pending migrations and refuses (code 3) unless the *last* one is exactly the named
   target — so a migration that shipped in this image but wasn't named is never silently applied, and
   naming an earlier migration than what's actually last pending is refused rather than silently
   under-applying.
4. If nothing is pending, reports "already up to date" and exits 0 — safe to re-run.
5. Otherwise holds a PostgreSQL session-level advisory lock (`pg_try_advisory_lock`) for the duration
   of the migration, so two operators (or two machines) invoking this at the same moment can't race;
   the loser refuses cleanly (code 3) instead of migrating concurrently.
6. Applies the migrations via `context.Database.MigrateAsync()`, the same EF Core API `dotnet ef
   database update` itself calls underneath.

Exit codes: `0` = up to date or migrated successfully, `1` = failure applying migrations (the
database may be partially migrated — do not retry blindly; inspect
`stepin.__ef_migrations_history` first), `2` = couldn't connect, `3` = a safety precondition refused
the run (nothing was written), `4` = the target migration id argument was missing.

It is dispatched the same way `aidx-init-owner` is, in `Program.cs`, before the web host starts: never
an HTTP endpoint, never run by the container entrypoint, startup, or health checks, and never
triggered automatically by an ordinary deploy — a command only runs when its name is passed as a
`fly ssh console -C` argument, so applying migrations can never happen as a side effect of booting or
redeploying the API. Tested locally against disposable Testcontainers Postgres instances
(`MigrateDatabaseCommandTests`): unreachable database, unrecognised target id, naming an earlier
migration than what's actually pending, a database with an applied migration this build doesn't
recognise, a full successful run, and a safe idempotent re-run.

**Not configured as a Fly `release_command`:** a release command's string is fixed in `fly.toml` and
runs unconditionally on every future deploy, which can't express "name the specific migration you
reviewed for *this* release" — the whole point of the mandatory argument. Wiring this up automatically
would either hardcode a single migration id that goes stale the moment the next migration ships, or
silently fall back to "latest," defeating the safety gate. Until a safer way to parameterise a release
command per-release exists, run this manually, once, per release that actually adds a migration.

**Verify after running:** the command's own output already reports applied/pending counts and the
final migration id; independently confirm via a read-only query (same `fly proxy`/`fly ssh console`
access): `SELECT "MigrationId" FROM stepin.__ef_migrations_history ORDER BY 1;` (the table name and
schema are fixed in code — see `DependencyInjection.cs`'s `npgsql.MigrationsHistoryTable(...)` call —
never assume the EF Core default of `public.__EFMigrationsHistory`).

### Fallback: `fly proxy` + `dotnet ef database update`

If the operator command above is unavailable (e.g. before it was added, or the deployed image
predates it), apply migrations from a local checkout instead:

```bash
# from apps/api, with the production connection supplied via environment, through `fly proxy`
dotnet ef migrations list --project src/StepIn.Infrastructure --startup-project src/StepIn.Api
dotnet ef database update 20261005021348_AddAidxFoundations \
  --project src/StepIn.Infrastructure --startup-project src/StepIn.Api
```

Verify: `dotnet ef migrations list` shows `20261005021348_AddAidxFoundations` as applied, and
`stepin.__ef_migrations_history` has the row. Then check the schema has the `aidx` schema and `Jobs.Category`.

> **Unverified:** this command has not been run against a restored copy of production. Run it
> against a restored snapshot first. If the environment doesn't pick up the connection override, stop
> and report it rather than pointing the command at production by another route.

## 3. Release the API

```bash
fly deploy --build-arg GIT_SHA=$(git rev-parse HEAD) -a stepin-api
```

The `GIT_SHA` build argument becomes the `org.opencontainers.image.revision` image label, so later
checks can match the image to source. Images built before this label have no commit.

Verify: `/health/live` returns healthy, and `/health/ready` returns healthy (it checks the database).

## 4. Create the AIDX system owner

Only after the migration and API release. The command is `aidx-init-owner`. It runs inside the
production container, so it uses the production configuration and no secret leaves the machine:

```bash
fly ssh console -a stepin-api -C "dotnet StepIn.Api.dll aidx-init-owner"
```

Expected output and exit codes:

| Output | Exit | Meaning |
|---|---|---|
| `AIDX system ownership initialized successfully.` | 0 | Created the owner (or completed a partial one). |
| `AIDX system ownership already exists and is consistent.` | 0 | Nothing written. Safe to rerun. |
| `AIDX system ownership is inconsistent.` / `No changes were made.` | 2 | Refused. Investigate before anything else. |
| `... initialisation failed (<ExceptionType>)` | 1 | Database or configuration problem. Check the logs. |

> **Unverified:** `fly ssh console -C` returning the command's exit code has not been checked. Confirm
> the exit status from the output and from `echo $?` on a non-production check first.

Verify the owner afterwards with a read-only query. Expect exactly one `system:aidx-lab` user, suspended,
with no role, one recruiter profile for it, and one company named `AIDX Lab`. A Research job owned by
this profile is an AIDX opportunity. No Career job may be owned by it.

If the command reports inconsistent state, stop. Do not repair it by hand. The service refuses on
purpose, because silent repair could mis-attribute opportunities.

## 5. Frontend release

Deploy the Vercel production build for the same commit. Promote only after the preview build passes the
smoke tests.

## Resume storage

Resume files are written through `IResumeStorage`, selected by `Storage__Provider` (`Local`, the
default, or `Supabase`). Both options keep resumes off the container's own disk-durability story —
read the one that applies to the current deployment.

### Option A — local disk + Fly volume (not currently attached)

Resume files are written to `Storage__ResumeRootPath` (local disk, see `LocalDiskResumeStorage`). The
image has no persistent storage, so resumes are lost on redeploy unless a volume is mounted.

Setup, once, before the first deploy that contains the `[[mounts]]` block in `fly.toml`:

```bash
fly volumes create stepin_resumes --region syd --size 1 -a stepin-api
fly secrets set Storage__ResumeRootPath=/app/data/resumes -a stepin-api
```

Keep one machine. A volume attaches to one machine.

Verify after the deploy:

- Upload a resume as a test candidate, then download it. It must succeed.
- Confirm the runtime user can write. The image creates `/app/data/resumes` as user `app`, but a volume
  mounted over `/app/data` may come up owned by root. If writes fail with a permission error, the
  volume needs ownership fixed for user `app` before release. Do not run the container as root to work
  around it.

**Superseded — see Option B below.** As of a read-only `fly secrets list -a stepin-api` check on
2026-10-13, `Storage__Provider` and all three `Storage__Supabase__*` keys are present and show
`Deployed`, which is consistent with Option B being the active configuration — but a secret's
*name* being set is not proof of its *value*; the actual provider string was not and should not be
printed. Confirm the effective provider from application logs or a live round-trip test if this
needs re-confirming, rather than assuming either option from this document alone.

### Option B — Supabase Storage (`SupabaseResumeStorage`)

**Status as of 2026-10-13 (read-only verification):** `Storage__Provider`, `Storage__Supabase__Url`,
`Storage__Supabase__ServiceKey`, and `Storage__Supabase__Bucket` are all present on `stepin-api` and
`Deployed` per `fly secrets list` (names/digests only — values were never requested or printed). This
corroborates the live-verification evidence recorded against commit `be8da5b`'s message (upload,
download, delete all passed against a real private bucket). It does **not** by itself prove the
*currently running* machine image has that fix: `fly releases -a stepin-api` shows the latest
deployed release is **v17**, dated **2026-10-11 13:06**, which predates `be8da5b` (**2026-10-12
00:04**, the commit that fixed `OpenReadAsync`/`DeleteAsync`'s handling of Supabase's actual
not-found response shape). In other words: the fix is on `origin/master`, but there is no evidence
a `v18` was ever deployed to apply it. Confirm the running image's commit (`org.opencontainers.image.revision`
label, if the running image carries one) before treating the fix as live.

`fly status -a stepin-api` on the same date showed both machines `stopped` (app-level `suspended`),
consistent with `fly.toml`'s `auto_stop_machines = 'stop'` / `min_machines_running = 0` — this is the
app's own idle scale-to-zero behavior, not an incident. A direct `GET /health/live` and
`GET /health/ready` against `https://stepin-api.fly.dev` both returned **200** (the first request
triggering the expected auto-start), and `https://stepin-psi.vercel.app/` returned **200** — the
production API and frontend are both reachable and healthy as of this check.

Persists resumes in a private Supabase Storage bucket over its plain HTTP object API, so they survive
every redeploy, container replacement, and restart. No Fly volume is involved; `stepin_resumes` is left
untouched either way.

**Prerequisites, done once, outside this repo:**

1. Create a Supabase project (any region is fine; same-region as `syd` reduces latency but isn't required).
2. Create a **private** bucket (e.g. `resumes`). Do not make it public.
3. Copy the project URL and the **service-role** key (not the anon/public key) from the project's API
   settings. The service-role key bypasses Row Level Security — treat it exactly like a database password.

**Activate, as a Fly secrets deploy:**

```bash
fly secrets set \
  Storage__Provider=Supabase \
  Storage__Supabase__Url=https://<project-ref>.supabase.co \
  Storage__Supabase__ServiceKey=<service-role key> \
  Storage__Supabase__Bucket=resumes \
  -a stepin-api
```

The app validates these three Supabase keys **at startup**: if `Storage__Provider=Supabase` and any of
`Url`/`ServiceKey`/`Bucket` is missing, the app fails to start with a named `InvalidOperationException`
rather than failing a candidate's first upload. Deploy after setting the secrets, then verify `/health/live`
comes back healthy (a failed start shows as the deploy never reaching a healthy machine).

**Verify after the deploy:**

- Upload a resume as a test candidate, then download it through the existing API. Bytes must match.
- Confirm the Supabase dashboard's bucket shows the new object, under a server-generated
  `{32-hex-chars}.{ext}` key — never the candidate's real filename.
- Redeploy the API (even with no code change) and confirm the same resume still downloads — this is
  the actual reliability property this option exists for.

**Cost/limits:** check the current Supabase plan's storage and bandwidth limits against the 5 MB-per-resume
cap before relying on this at scale; pricing has changed over time and isn't fixed here.

### Rollback between the two options

- **Supabase → Local:** `fly secrets set Storage__Provider=Local -a stepin-api` (or unset it) and deploy.
  Any resume uploaded while Supabase was active stays in Supabase — switching the provider does not move
  files. A candidate who uploaded under Supabase cannot be served from local disk after this switch; their
  `ResumeStorageKey` only resolves against whichever provider wrote it. Decide the data story before
  switching a production deployment that has real resumes.
- **Local → Supabase:** the reverse of the above — existing local-disk resumes are not migrated
  automatically. There is no file to migrate today (production has zero `JobApplications` rows as of the
  last read-only check), so this is currently a clean switch either direction.
- **Supabase outage:** uploads/downloads fail with a safe, logged 500 (via `GlobalExceptionHandler`) —
  not a silent data loss. No automatic fallback to local disk exists; this is a deliberate choice, since a
  silent fallback would mean some resumes land on ephemeral disk without anyone noticing.
- **Orphaned objects:** a failed `DeleteAsync` (e.g. during the duplicate-application cleanup path) is
  logged as a warning, not thrown, so it never masks the original error — but it means an orphaned object
  can exist in the bucket. Reconcile periodically by comparing bucket contents against
  `stepin."JobApplications"."ResumeStorageKey"` if this ever needs auditing.

## Rollback

- **Frontend:** promote the previous Vercel production deployment from the dashboard.
- **API:** redeploy the previous image: `fly releases -a stepin-api`, then deploy that image. Image rollback
  does not touch the database.
- **Database:** no destructive rollback. Do not run the migration's `Down()`. The additive schema is safe
  to leave in place. The old API still works against it (see Compatibility).
- **Owner initialisation:** the command writes nothing on refusal, and rerunning it is safe.
- **Restore from snapshot:** only as a last resort, and only with the owner's approval, since it discards
  data written after the snapshot.

Partial failures:

- API deployed, frontend failed: the old frontend still works against the new API. Fix the frontend and redeploy it.
- Frontend deployed, API failed: roll back the API. The frontend shows errors but writes nothing.
- Migration applied, API deploy failed: keep the migrated schema and redeploy the previous image.

## Smoke tests

Run in production after step 5, with a real account for each role:

- [ ] `/aidx`, `/aidx/research`, `/aidx/projects`, `/aidx/people`, `/aidx/publications`, `/aidx/news`, `/aidx/events`, `/aidx/opportunities`, `/aidx/about`, `/aidx/contact` load.
- [ ] StepIn: home, jobs, internships, companies, dashboard, discovery, saved jobs.
- [ ] Candidate, recruiter, and admin sign-in work. Protected routes redirect when signed out.
- [ ] `/admin/aidx/opportunities` loads for an admin (503 before step 4, normal after).
- [ ] Admin creates a research opportunity, links a published project, publishes it. It appears on `/aidx/opportunities`.
- [ ] The opportunity opens at `/jobs/{id}`, a candidate applies, the recruiter sees the application and updates its status.
- [ ] Resume upload and download work (see Resume storage).

### AIDX authoring/review/publish (this release — needs a real Clerk session per role)

- [ ] As the newly-named LabManager: `/aidx/manage/memberships` loads, search finds a real account,
      granting a Contributor membership succeeds.
- [ ] As that Contributor: `/aidx/workspace/projects/new` creates a draft with a milestone, a
      research area, a technology, a team member, and an uploaded cover image; `/aidx/workspace`
      lists it; Submit for review succeeds.
- [ ] As an Editor (grant a second membership first): the item appears in `/aidx/manage`, opening
      its review detail page shows the real content (not just a title), Request changes with a
      comment succeeds.
- [ ] As the Contributor again: the "changes requested" banner shows the real comment; edit and
      resubmit succeeds.
- [ ] As the Editor: Approve succeeds; the project's public page (`/aidx/projects/{slug}`) shows the
      milestone, research area, technology, team member, and image.
- [ ] Anonymous: the draft's image URL and the authoring GET route both refuse access before
      publish (401/403), and the published image is reachable after.
- [ ] `/aidx/manage/memberships` is unreachable (redirects) for an Editor who is not also a
      LabManager.

### Career Intelligence (after the 4 migrations above are applied and this release is deployed)

- [ ] `/health/live` and `/health/ready` both healthy.
- [ ] `GET /api/v1/jobs/matches` as an applicant: zero-skill jobs report `Unscored`, not 0%.
- [ ] `GET /api/v1/aidx/research-matches` as an applicant loads without error.
- [ ] `/dashboard/career-gps`: create a goal, add a roadmap item, see a skill gap (or an honest
      "no target job" state).
- [ ] `/dashboard/portfolio`: create a project (defaults Private), set it Public.
- [ ] `/dashboard/interview-practice`: start a session, answer a question, see rubric-based feedback
      labelled as such (not AI-generated).
- [ ] `/dashboard/experience-passport`: create a record, submit it. Confirm it reaches
      `/admin/verification` only once an authorized Admin exists (see the prerequisite above) —
      otherwise this step is explicitly blocked, not silently skipped.
- [ ] Recruiter application detail (`GET /api/v1/recruiter/applications/{id}`) still loads — this is
      the existing endpoint that now also queries the 2 new tables; confirm it returns 200 with
      `portfolioProjects`/`experienceRecords` present (possibly empty arrays), not a 500.
- [ ] A different candidate cannot see another candidate's Private portfolio/experience records
      (navigate directly by id; expect 404).

## Career Intelligence release (Phases 1–7)

As of 2026-10-13, `origin/master` is at `c4a0e9e` — the 8 commits adding Transparent Job Match,
Research-to-Career Bridge, Career GPS, Proof-of-Skills Portfolio, Interview Practice Studio,
Experience Passport, and an integration-audit fix pass have been **pushed**, verified by a fresh
438/438 backend test run and clean frontend typecheck/lint/build at that exact commit. They have
**not** been deployed or migrated to production.

Exactly **4** new migrations are pending in production, not 5 — a figure corrected during
verification: `AddAidxFoundations` (`20261005021348`) is an ancestor of the *previous* remote tip
(`be8da5b`) and has been live across multiple prior releases (confirmed via git ancestry and the
production API already serving AIDX routes; not independently confirmed by a direct production
schema query). The 4 actually pending are, in order:

1. `20261011140004_AddCareerGoals`
2. `20261012000657_AddPortfolioProjects`
3. `20261012031355_AddInterviewPractice`
4. `20261012131143_AddExperiencePassport`

Apply them with the `migrate-database` operator command above, naming
`20261012131143_AddExperiencePassport` as the target — it applies all 4 in order in one invocation.

Additional prerequisites specific to this release, beyond the AIDX owner step above:

- **No production Admin account is known to exist.** The Experience Passport verification queue
  (`/admin/verification`, `RequireAdmin`-gated) has no reviewer until one is deliberately promoted —
  the same documented `UPDATE ... SET "Role" = 'Admin'` step used for every other admin-only feature
  in this codebase. Do not create one as a side effect of deploying; it is a separate, explicit
  operator action, taken only after confirming who the production admin should be.
- These 4 migrations are additive only (new tables, no altered existing columns) — same
  compatibility shape as `AddAidxFoundations` above, so the same "migrate first, then release the
  API" ordering applies, and the old API continues to work against the migrated schema in the
  window between the two.
- **Unlike `AddAidxFoundations`, this release's compatibility is one-directional, not safe in the
  other order.** The new `RecruiterApplicationEndpoints.GetApplicationAsync`/`UpdateStatusAsync`
  unconditionally query `PortfolioProjects` and `ExperienceRecords` — tables that don't exist until
  these 4 migrations are applied. Deploying the new API image before migrating would make the
  *existing, currently-working* recruiter application-detail endpoint fail for every request, not
  just the new Career Intelligence routes. Migrate first. Always.
- Re-run the Resume storage verification above after this release if resume-adjacent code changes —
  it did not change in this phase, so no new verification is required solely for Career
  Intelligence, but confirm the running image's commit before assuming the earlier Supabase fix
  (`be8da5b`) is included.

**Update, 2026-10-14 (AIDX Lab publishing — Milestones 1–2):** two more
migrations are now pending on top of the 4 above:
`20261014145029_AddAidxMembership` (Milestone 1 — purely additive: new
`AidxMemberships`/`AidxMembershipAuditEntries` tables, nullable
`CreatedByMembershipId` columns on existing AIDX content tables) and
`20261014153949_AddAidxEditorialWorkflow` (Milestone 2 — additive schema
plus a one-time data backfill; see
[aidx-migration-plan.md](aidx-migration-plan.md) for the expand/backfill
details and the honest rollback-safety boundary). Both carry the same
"migrate first" ordering and old-API compatibility as the 4 above. Apply
all 6 in one invocation by naming `20261014153949_AddAidxEditorialWorkflow`
as the `migrate-database` target instead of
`20261012131143_AddExperiencePassport`. See
[aidx-permissions.md](../architecture/aidx-permissions.md) for the bootstrap
step (`aidx-bootstrap-manager`) required before any AIDX lab membership can
be granted — it is a separate, explicit, one-time operator action, not run
automatically by this or any migration.

**Update, 2026-10-17 (AIDX authoring/media release — this release):** four
more migrations are now pending on top of the 6 above:
`20261014222839_AddAidxCollaboration` (saved items, collaboration
enquiries, event registration — additive), `20261015051309_AddAidxResearcherImageAltText`,
`20261015092429_AddAidxNewsEventImageAltText`, and
`20261015114655_AddAidxProjectImage` (each adds a nullable alt-text column
and, for the last one, a nullable image-key column, plus an honest
placeholder backfill scoped to rows that already have an image — see each
migration file's own comment). All four carry the same additive shape and
"migrate first" ordering as every migration above. Apply all 10 pending
migrations in one invocation by naming `20261015114655_AddAidxProjectImage`
as the `migrate-database` target. **New this release:** production must
also have `Storage__Supabase__AidxMediaBucket` set before the API deploys
— see the readiness snapshot at the top of this document — and the first
AIDX LabManager must be named via `aidx-bootstrap-manager` (distinct from
`aidx-init-owner` in §4, which creates the system *content-ownership*
account, not a human membership).

## Monitoring

Use the platform's own tooling (`fly logs -a stepin-api`, Vercel logs). Watch for 5xx responses, a spike
in 401/403 on AIDX admin routes, and 503 from the AIDX opportunity admin routes after release (expected
only before step 4).
