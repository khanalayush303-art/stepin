"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useClerk, useUser } from "@clerk/nextjs";
import type { LucideIcon } from "lucide-react";
import { LogOut } from "lucide-react";
import { Logo } from "@/components/layout/logo";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export interface DashboardNavItem {
  href: string;
  label: string;
  icon: LucideIcon;
  /** A real, current count — never a placeholder. */
  badge?: string;
  /**
   * The destination exists and is honest about its own state (e.g. "not available yet"), but
   * the feature itself isn't built — this renders a muted "Soon" badge instead of pretending
   * the link leads somewhere fully working, without removing navigability to that honest page.
   */
  comingSoon?: boolean;
}

export function DashboardShell({
  nav,
  navLabel,
  children,
}: {
  nav: DashboardNavItem[];
  navLabel: string;
  children: React.ReactNode;
}) {
  const pathname = usePathname();
  const { user } = useUser();
  const { signOut } = useClerk();
  const [signingOut, setSigningOut] = React.useState(false);
  const [signOutError, setSignOutError] = React.useState(false);

  async function onSignOut() {
    if (signingOut) return; // Button is already disabled while loading; this also covers the Enter-key repeat case.
    setSigningOut(true);
    setSignOutError(false);
    try {
      // No destination is passed here — Clerk's own `afterSignOutUrl` (set on
      // ClerkProvider) is what navigates once the session is *actually* cleared, and that
      // navigation also triggers Clerk's own router.refresh() to drop the Next.js Router
      // Cache. The previous version pushed its own router.push("/sign-in") as a signOut()
      // callback, which bypasses that coordination entirely (the callback replaces Clerk's
      // navigate step rather than running alongside it) and raced a separate, uncoordinated
      // client-side transition against Clerk's own session-teardown navigation — which is
      // why Logout could land back on the dashboard instead of signing out.
      await signOut();
    } catch {
      setSignOutError(true);
      setSigningOut(false);
    }
  }

  return (
    <div className="flex min-h-screen flex-col lg:flex-row">
      <aside className="flex flex-col border-b border-border bg-surface lg:w-[260px] lg:shrink-0 lg:border-b-0 lg:border-r">
        <div className="flex-1 p-4 lg:sticky lg:top-0">
          <div className="px-2 pb-4 pt-2">
            <Logo />
          </div>
          <nav aria-label={navLabel}>
            <ul className="flex gap-1 overflow-x-auto lg:flex-col lg:overflow-visible">
              {nav.map((item) => {
                const active = pathname === item.href;
                const Icon = item.icon;
                return (
                  <li key={item.href} className="shrink-0 lg:shrink">
                    <Link
                      href={item.href}
                      aria-current={active ? "page" : undefined}
                      className={cn(
                        "flex h-control-md items-center gap-3 rounded-md px-3 text-small",
                        "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring",
                        active
                          ? "bg-primary-subtle font-medium text-primary"
                          : "text-foreground-secondary hover:bg-muted"
                      )}
                    >
                      <Icon
                        className={cn(
                          "size-[18px] shrink-0",
                          active ? "text-primary" : "text-muted-foreground"
                        )}
                        aria-hidden="true"
                      />
                      <span className="flex-1 whitespace-nowrap">{item.label}</span>
                      {item.comingSoon ? (
                        <span className="rounded-full bg-muted px-1.5 py-0.5 text-caption text-muted-foreground">
                          Soon
                        </span>
                      ) : item.badge ? (
                        <span className="rounded-full bg-muted px-1.5 py-0.5 text-caption text-foreground-secondary">
                          {item.badge}
                        </span>
                      ) : null}
                    </Link>
                  </li>
                );
              })}
            </ul>
          </nav>
        </div>

        <div className="flex flex-col gap-2 border-t border-border p-4">
          {signOutError ? (
            <Alert tone="error" title="Couldn't sign you out. Please try again." />
          ) : null}
          <div className="flex items-center justify-between gap-2">
            <div className="min-w-0">
              <p className="truncate text-small font-medium text-foreground">
                {user ? user.fullName ?? user.firstName : "…"}
              </p>
              <p className="truncate text-caption text-muted-foreground">
                {user?.primaryEmailAddress?.emailAddress}
              </p>
            </div>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              aria-label="Sign out"
              onClick={onSignOut}
              loading={signingOut}
            >
              <LogOut aria-hidden="true" />
            </Button>
          </div>
        </div>
      </aside>

      <main id="main" className="min-w-0 flex-1 p-6 lg:p-10">
        {children}
      </main>
    </div>
  );
}
