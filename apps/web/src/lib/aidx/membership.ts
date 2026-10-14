import "server-only";
import { redirect } from "next/navigation";
import { auth } from "@clerk/nextjs/server";
import { apiOrigin, getCurrentAppUserResult } from "@/lib/auth/server";

export type AidxLabRole = "Contributor" | "Editor" | "LabManager";

interface AidxMembershipMeResponse {
  hasMembership: boolean;
  role: AidxLabRole | null;
  status: string | null;
  scopedResearchAreaId: string | null;
}

export interface AidxAccess {
  isPlatformAdmin: boolean;
  labRole: AidxLabRole | null;
  scopedResearchAreaId: string | null;
}

async function fetchMembership(): Promise<AidxMembershipMeResponse | null> {
  const { getToken } = await auth();
  const token = await getToken();
  if (!token) {
    return null;
  }

  try {
    const response = await fetch(`${apiOrigin()}/api/v1/aidx/membership/me`, {
      headers: { Authorization: `Bearer ${token}` },
      cache: "no-store",
    });
    if (!response.ok) {
      return null;
    }
    return (await response.json()) as AidxMembershipMeResponse;
  } catch {
    return null;
  }
}

/**
 * Gated by "is this a real lab contributor or a platform Admin" — Admin
 * implicitly satisfies every AIDX check with no membership row at all (see
 * docs/architecture/aidx-permissions.md), the same rule the backend applies.
 * Redirects to sign-in if signed out, to `/aidx` with a clear reason
 * otherwise — there is no separate "not a lab member" page; the public AIDX
 * site itself is that destination, since someone with no lab access still
 * has a perfectly normal place to land.
 */
export async function requireAidxWorkspaceAccess(unauthenticatedUrl: string): Promise<AidxAccess> {
  const appUser = await getCurrentAppUserResult();

  if (appUser.status === "signed-out") {
    redirect(unauthenticatedUrl);
  }
  if (appUser.status === "unavailable") {
    redirect("/service-unavailable");
  }
  if (appUser.status === "disabled") {
    redirect("/account-disabled");
  }

  const isPlatformAdmin = appUser.status === "active" && appUser.user.role === "Admin";
  const membership = await fetchMembership();

  if (!isPlatformAdmin && !membership?.hasMembership) {
    redirect("/aidx?reason=no-lab-access");
  }

  return {
    isPlatformAdmin,
    labRole: membership?.hasMembership ? membership.role : null,
    scopedResearchAreaId: membership?.scopedResearchAreaId ?? null,
  };
}

/** Workspace access, further restricted to Editor/LabManager (or platform Admin) — Contributors
 * have no reviewer rights and no reason to reach /aidx/manage. */
export async function requireAidxManageAccess(unauthenticatedUrl: string): Promise<AidxAccess> {
  const access = await requireAidxWorkspaceAccess(unauthenticatedUrl);

  if (!access.isPlatformAdmin && access.labRole === "Contributor") {
    redirect("/aidx/workspace?reason=not-a-reviewer");
  }

  return access;
}
