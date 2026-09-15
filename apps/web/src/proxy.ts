import { NextResponse, type NextRequest } from "next/server";
import { dashboardPathForRole, type AuthUser, type UserRole } from "@/lib/auth/types";

/**
 * UX convenience only. The real authorization boundary is the API's
 * [Authorize]/role-policy checks — this proxy asks the backend who the
 * cookie belongs to and redirects accordingly, but never grants access on its
 * own: a request that skipped this file entirely would still be refused by
 * the API for anything protected.
 */
const PROTECTED_ROLES: Record<string, UserRole> = {
  "/dashboard": "Applicant",
  "/recruiter": "Recruiter",
  "/admin": "Admin",
};

const AUTH_ROUTES = ["/sign-in", "/register"];

function apiOrigin(): string {
  return process.env.API_INTERNAL_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";
}

async function getUser(request: NextRequest): Promise<AuthUser | null> {
  const cookie = request.headers.get("cookie");
  if (!cookie) return null;

  try {
    const response = await fetch(`${apiOrigin()}/api/v1/auth/me`, { headers: { cookie } });
    if (!response.ok) return null;
    return (await response.json()) as AuthUser;
  } catch {
    // The API being unreachable should not lock every visitor out of public
    // pages elsewhere in the app — protected routes below still fail closed.
    return null;
  }
}

export async function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;

  const protectedPrefix = Object.keys(PROTECTED_ROLES).find(
    (prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`),
  );
  const isAuthRoute = AUTH_ROUTES.includes(pathname);

  if (!protectedPrefix && !isAuthRoute) {
    return NextResponse.next();
  }

  const user = await getUser(request);

  if (protectedPrefix) {
    if (!user) {
      const signInUrl = new URL("/sign-in", request.url);
      signInUrl.searchParams.set("returnTo", pathname);
      return NextResponse.redirect(signInUrl);
    }

    if (user.role !== PROTECTED_ROLES[protectedPrefix]) {
      return NextResponse.redirect(new URL(dashboardPathForRole(user.role), request.url));
    }
  }

  if (isAuthRoute && user) {
    return NextResponse.redirect(new URL(dashboardPathForRole(user.role), request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/dashboard/:path*", "/recruiter/:path*", "/admin/:path*", "/sign-in", "/register"],
};
