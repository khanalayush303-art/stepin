import "server-only";
import { auth } from "@clerk/nextjs/server";
import type { AuthUser } from "./types";

function apiOrigin(): string {
  return process.env.API_INTERNAL_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";
}

/** Server-side only: fetches the app-level profile (role, accountStatus) for the signed-in Clerk user. */
export async function getCurrentAppUser(): Promise<AuthUser | null> {
  const { getToken } = await auth();
  const token = await getToken();

  if (!token) {
    return null;
  }

  const response = await fetch(`${apiOrigin()}/api/v1/auth/me`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  if (!response.ok) {
    return null;
  }

  return (await response.json()) as AuthUser;
}
