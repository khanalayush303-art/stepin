# Phase 3.1 — Production Resume Storage Verification

**Status: fix applied and verified in production.**
See the Phase 3.1 checkpoint reports for the full findings. This document is
the lasting reference for how resume storage actually behaves.

## How `LocalDiskResumeStorage` resolves its root directory

`apps/api/src/StepIn.Infrastructure/Storage/LocalDiskResumeStorage.cs`:

```csharp
private readonly string _root = configuration["Storage:ResumeRootPath"]
    ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "resumes");
```

Two branches:
- **Configured**: `Storage:ResumeRootPath` (env var form: `Storage__ResumeRootPath`) is set — the app writes there.
- **Fallback**: unset — the app writes to `{AppContext.BaseDirectory}/App_Data/resumes`, which for the published container is `/app/App_Data/resumes`.

## Local development and Docker Compose — correct today

`docker-compose.yml` sets `Storage__ResumeRootPath: /app/data/resumes` and mounts a named volume (`resume-data`) at that same path. The `Dockerfile` prepares that exact directory (`mkdir -p /app/data/resumes && chown -R app:app /app/data`) before switching to the non-root `app` user. This is correctly configured and was not changed by Phase 3.1.

`dotnet run` locally (outside Docker) uses the fallback path under the repo's own `bin/` output directory — already covered by the existing `**/bin/` `.gitignore` pattern.

## Production (Fly.io) — root cause found, then fixed

**Before the fix**, verified live, read-only, via `flyctl secrets list -a stepin-api` and `flyctl ssh console -a stepin-api`:

- No `Storage__ResumeRootPath` secret was set in production → the app used the **fallback** path.
- The fallback path, `/app/App_Data/resumes`, **did not exist** and could not be created by the running process: `/app` is `root:root`, mode `755`; the `dotnet StepIn.Api.dll` process runs as the non-root `app` user (confirmed via `ps aux`).
- The directory the Dockerfile *did* correctly prepare, `/app/data/resumes`, was `app:app`, mode `755` — writable — but the app wasn't configured to use it in production.
- No Fly volume is attached to `stepin-api` (`flyctl volumes list` returns zero rows) — so even once pointed at the correct path, resumes written there live only on the container's local disk and will not survive a machine replacement or redeploy.

## The fix — applied

Set, as a Fly secret on `stepin-api`:

```
Storage__ResumeRootPath = /app/data/resumes
```

This points production at the exact directory the Dockerfile already prepares and the `app` user already owns — mirroring the value `docker-compose.yml` already uses for local Docker testing. **No code change was required**; `LocalDiskResumeStorage`'s configured-path branch already did the right thing once this value existed. Setting the secret triggered Fly's automatic rolling redeploy.

**Verified after the fix** (live, read-only + one harmless, self-cleaned write test):
- `flyctl secrets list` shows `Storage__ResumeRootPath` as `Deployed` (name/digest only — value never printed).
- The new machine started cleanly: `StepIn API starting in Production` → `Application started` → a real `GET /api/v1/jobs` request served `200` shortly after. No storage-related error anywhere in the startup or runtime logs.
- As the exact `app` user the process runs as (via `su app` inside the container), a test file was written to, read back from, and deleted from `/app/data/resumes` — proving the directory is genuinely writable by the running process, not just by `stat`'s reported permission bits.
- **Not verified**: a full end-to-end upload through the real `/api/v1/jobs/{jobId}/applications` HTTP endpoint with a genuine Clerk-authenticated candidate session. Obtaining a real Clerk session non-interactively from this environment isn't feasible, consistent with the Clerk dev-browser/Playwright limitation already documented elsewhere in this project. The infrastructure-level write test above is the strongest verification available without that session.

## What this fix does and does not solve

- **Solves**: resume upload/retrieval in production (the permission error is gone, verified above).
- **Does not solve**: durability across a Fly machine replacement or redeploy — `/app/data` is still local container disk with no attached volume. That remains the same, already-documented, deliberately-deferred limitation `LocalDiskResumeStorage`'s own doc comment describes. Closing that gap means either attaching a Fly volume to `/app/data` or moving to object storage — both are infrastructure/architecture decisions requiring explicit product-owner approval, out of scope for Phase 3.1.

> Immediate production resume functionality has been restored using the existing local-disk storage configuration. Long-term durable resume storage remains a separate infrastructure decision.

## Future storage recommendation

A future storage phase should evaluate persistent object storage, kept behind the existing `IResumeStorage` abstraction so the application layer stays independent of whichever provider is eventually chosen:

```
IResumeStorage
      ↓
Persistent Object Storage
      ↓
Resume Object
```

No provider is selected or configured here — that evaluation and decision belongs to a separately planned, tested, and approved future phase.

## How to test storage locally

`apps/api/tests/StepIn.Api.Tests/LocalDiskResumeStorageTests.cs` exercises `LocalDiskResumeStorage` directly against a temporary directory (never the real dev or production filesystem): configured-root round-trip, missing-key read, delete-then-delete-again, path-traversal-key defense, and the exact fallback-path formula. Run via `dotnet test` from `apps/api`.
