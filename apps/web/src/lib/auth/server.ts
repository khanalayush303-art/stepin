import "server-only";
import { auth } from "@clerk/nextjs/server";
import type { AuthUser } from "./types";

function apiOrigin(): string {
  // `||`, not `??` — an env var set to "" (as opposed to genuinely unset) is
  // still falsy here, so it doesn't win over the next fallback the way it
  // would with `??`. Deployed environments (e.g. Vercel) can very easily end
  // up with one of these set-but-blank rather than absent.
  return process.env.API_INTERNAL_BASE_URL || process.env.NEXT_PUBLIC_API_BASE_URL || "http://localhost:5080";
}

/**
 * Server-side only: fetches the app-level profile (role, accountStatus) for
 * the signed-in Clerk user. Best-effort — every caller already treats a null
 * return as "couldn't determine the app-level profile" (no role yet, or the
 * API is unreachable), so a bad/unreachable API origin must degrade to null
 * rather than throw and crash the page.
 */
export async function getCurrentAppUser(): Promise<AuthUser | null> {
  const { getToken } = await auth();
  const token = await getToken();

  if (!token) {
    return null;
  }

  try {
    const response = await fetch(`${apiOrigin()}/api/v1/auth/me`, {
      headers: { Authorization: `Bearer ${token}` },
      cache: "no-store",
    });

    if (!response.ok) {
      return null;
    }

    return (await response.json()) as AuthUser;
  } catch {
    return null;
  }
}
