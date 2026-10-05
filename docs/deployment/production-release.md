# Production release runbook (StepIn + AIDX Lab)

Status: **pre-production.** Nothing in this document has been run against production. Each step is
an operator action, in the order below.

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
way production gets the AIDX schema.

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

## Monitoring

Use the platform's own tooling (`fly logs -a stepin-api`, Vercel logs). Watch for 5xx responses, a spike
in 401/403 on AIDX admin routes, and 503 from the AIDX opportunity admin routes after release (expected
only before step 4).
