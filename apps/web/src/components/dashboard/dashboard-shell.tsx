"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import type { LucideIcon } from "lucide-react";
import { LogOut } from "lucide-react";
import { Logo } from "@/components/layout/logo";
import { Button } from "@/components/ui/button";
import { logout } from "@/lib/auth/api";
import { useSession } from "@/lib/auth/session-context";
import { cn } from "@/lib/utils";

export interface DashboardNavItem {
  href: string;
  label: string;
  icon: LucideIcon;
  badge?: string;
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
  const router = useRouter();
  const { user, refresh } = useSession();
  const [signingOut, setSigningOut] = React.useState(false);

  async function onSignOut() {
    setSigningOut(true);
    try {
      await logout();
    } finally {
      await refresh();
      router.push("/sign-in");
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
                      {item.badge ? (
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

        <div className="flex items-center justify-between gap-2 border-t border-border p-4">
          <div className="min-w-0">
            <p className="truncate text-small font-medium text-foreground">
              {user ? `${user.firstName} ${user.lastName}` : "…"}
            </p>
            <p className="truncate text-caption text-muted-foreground">{user?.email}</p>
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
      </aside>

      <main id="main" className="min-w-0 flex-1 p-6 lg:p-10">
        {children}
      </main>
    </div>
  );
}
