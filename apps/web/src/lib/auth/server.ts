import "server-only";
import { redirect } from "next/navigation";
import { auth } from "@clerk/nextjs/server";
import { dashboardPathForRole, type AuthUser, type UserRole } from "./types";

export function apiOrigin(): string {
  // `||`, not `??` — an env var set to "" (as opposed to genuinely unset) is
  // still falsy here, so it doesn't win over the next fallback the way it
  // would with `??`. Deployed environments (e.g. Vercel) can very easily end
  // up with one of these set-but-blank rather than absent.
  return process.env.API_INTERNAL_BASE_URL || process.env.NEXT_PUBLIC_API_BASE_URL || "http://localhost:5080";
}

/**
 * Every condition a layout needs to tell apart before deciding where to send
 * someone — collapsing these into `AuthUser | null` previously made "no role
 * yet", "disabled account", and "the API is down" all look identical, so a
 * genuine outage was indistinguishable from an incomplete account setup and
 * every layout sent the visitor to /account-setup regardless of which one it
 * actually was.
 *
 * - "signed-out": no Clerk session token at all.
 * - "no-role": a valid identity, synced, with no application role yet.
 * - "active": a valid identity with a role and an Active account.
 * - "disabled": a valid identity whose application account is Suspended —
 *   the backend returns 403 for this, never the user's own (possibly stale)
 *   role, so this case is only ever reached via that specific status code.
 * - "unavailable": the token exists but the API couldn't be reached, or
 *   returned something other than 200/403 — a real failure, not an
 *   authorization decision.
 */
export type CurrentAppUserResult =
  | { status: "signed-out" }
  | { status: "no-role"; user: AuthUser }
  | { status: "active"; user: AuthUser }
  | { status: "disabled" }
  | { status: "unavailable" };

/**
 * Server-side only: fetches the app-level profile (role, accountStatus) for
 * the signed-in Clerk user, as a typed result a layout can switch on instead
 * of guessing from a single nullable value. Always uncached (`no-store`): a
 * role or account-status change must take effect on the very next request,
 * never be served from a stale cache.
 */
export async function getCurrentAppUserResult(): Promise<CurrentAppUserResult> {
  const { getToken } = await auth();
  const token = await getToken();

  if (!token) {
    return { status: "signed-out" };
  }

  let response: Response;
  try {
    response = await fetch(`${apiOrigin()}/api/v1/auth/me`, {
      headers: { Authorization: `Bearer ${token}` },
      cache: "no-store",
    });
  } catch {
    // Network failure reaching the API — an outage, not an authorization decision.
    return { status: "unavailable" };
  }

  if (response.status === 403) {
    // The backend's explicit signal for "valid identity, disabled account" — never the
    // user's own role or profile data, by design (see AuthEndpoints.GetCurrentUserAsync).
    return { status: "disabled" };
  }

  if (!response.ok) {
    return { status: "unavailable" };
  }

  let user: AuthUser;
  try {
    user = (await response.json()) as AuthUser;
  } catch {
    return { status: "unavailable" };
  }

  return user.role ? { status: "active", user } : { status: "no-role", user };
}

/**
 * Shared by the three role-gated layouts (dashboard/recruiter/admin): resolves the typed result
 * and redirects for every status that isn't "a confirmed, active user holding exactly the
 * required role" — including distinct destinations for "disabled" and "unavailable", which
 * `!user?.role` used to send to /account-setup right alongside a genuinely roleless user. Callers
 * still run `auth.protect()` first for the signed-out case (consistent with each route's own
 * `unauthenticatedUrl`); the "signed-out" branch here is a defensive fallback, not the primary path.
 */
export async function requireAppUserWithRole(requiredRole: UserRole, unauthenticatedUrl: string): Promise<AuthUser> {
  const result = await getCurrentAppUserResult();

  switch (result.status) {
    case "signed-out":
      return redirect(unauthenticatedUrl);
    case "unavailable":
      return redirect("/service-unavailable");
    case "disabled":
      return redirect("/account-disabled");
    case "no-role":
      return redirect("/account-setup");
    case "active":
      // "active" is only ever returned when AuthUser.role is truthy (see
      // getCurrentAppUserResult) — the field stays typed as UserRole | null because the same
      // AuthUser shape is also used for "no-role", where it genuinely is null.
      if (result.user.role !== requiredRole) {
        return redirect(dashboardPathForRole(result.user.role!));
      }
      return result.user;
  }
}
