"use client";

import * as React from "react";
import Link from "next/link";
import { useAuth, useClerk, useUser } from "@clerk/nextjs";
import { Button } from "@/components/ui/button";
import { apiClient } from "@/lib/auth/client";
import { dashboardPathForRole, type AuthUser } from "@/lib/auth/types";

interface AidxMembershipMeResponse {
  hasMembership: boolean;
  role: "Contributor" | "Editor" | "LabManager" | null;
}

/**
 * Account-aware corner of the AIDX header. Four states, in order of what a
 * visitor is actually authorized for — never a fabricated "Manage AIDX" link
 * someone can't use. Logout reuses the same signOut() + afterSignOutUrl
 * flow as the StepIn dashboard shell (see dashboard-shell.tsx) — not
 * re-implemented here.
 */
export function AidxAccountNav() {
  const { isLoaded, isSignedIn, getToken } = useAuth();
  const { user } = useUser();
  const { signOut } = useClerk();
  const [appUser, setAppUser] = React.useState<AuthUser | null>(null);
  const [membership, setMembership] = React.useState<AidxMembershipMeResponse | null>(null);
  const [signingOut, setSigningOut] = React.useState(false);

  React.useEffect(() => {
    if (!isLoaded || !isSignedIn) {
      return;
    }

    let cancelled = false;
    (async () => {
      const token = await getToken();
      const [authResult, membershipResult] = await Promise.allSettled([
        apiClient.get<AuthUser>("/api/v1/auth/me", token),
        apiClient.get<AidxMembershipMeResponse>("/api/v1/aidx/membership/me", token),
      ]);
      if (cancelled) {
        return;
      }
      if (authResult.status === "fulfilled") {
        setAppUser(authResult.value);
      }
      if (membershipResult.status === "fulfilled") {
        setMembership(membershipResult.value);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [isLoaded, isSignedIn, getToken]);

  async function onSignOut() {
    if (signingOut) return;
    setSigningOut(true);
    try {
      await signOut();
    } catch {
      setSigningOut(false);
    }
  }

  if (!isLoaded) {
    return <span className="h-control-md w-24" aria-hidden="true" />;
  }

  if (!isSignedIn) {
    return (
      <Button variant="outline" size="sm" asChild>
        <Link href="/sign-in?returnTo=/aidx">Sign in</Link>
      </Button>
    );
  }

  const isAdmin = appUser?.role === "Admin";
  const canManage = isAdmin || membership?.role === "Editor" || membership?.role === "LabManager";
  const canContribute = isAdmin || !!membership?.hasMembership;

  return (
    <div className="flex flex-wrap items-center gap-3">
      {canManage ? (
        <Link
          href="/aidx/manage"
          className="rounded-sm text-small font-medium text-foreground-secondary underline-offset-4 hover:text-foreground hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          Manage AIDX
        </Link>
      ) : canContribute ? (
        <Link
          href="/aidx/workspace"
          className="rounded-sm text-small font-medium text-foreground-secondary underline-offset-4 hover:text-foreground hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          My AIDX workspace
        </Link>
      ) : null}

      {appUser?.role ? (
        <Link
          href={dashboardPathForRole(appUser.role)}
          className="rounded-sm text-small font-medium text-foreground-secondary underline-offset-4 hover:text-foreground hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          My dashboard
        </Link>
      ) : null}

      <span className="hidden text-small text-muted-foreground sm:inline">
        {user?.primaryEmailAddress?.emailAddress}
      </span>

      <Button variant="ghost" size="sm" onClick={onSignOut} loading={signingOut}>
        Sign out
      </Button>
    </div>
  );
}
