"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Menu, Search, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Logo } from "./logo";
import { cn } from "@/lib/utils";

const NAV = [
  { href: "/jobs", label: "Jobs" },
  { href: "/internships", label: "Internships" },
  { href: "/companies", label: "Companies" },
  { href: "/how-it-works", label: "How it works" },
];

export function SiteHeader() {
  const pathname = usePathname();
  const [open, setOpen] = React.useState(false);

  const isActive = (href: string) => pathname === href || pathname.startsWith(`${href}/`);

  return (
    <header className="sticky top-0 z-40 border-b border-border bg-surface">
      <a
        href="#main"
        className="sr-only-focusable absolute left-4 top-3 z-50 rounded-md bg-primary px-4 py-2 text-small font-semibold text-primary-foreground"
      >
        Skip to main content
      </a>

      <div className="container-page flex h-14 items-center justify-between gap-6 lg:h-[72px]">
        <div className="flex items-center gap-8">
          <Logo />
          <nav aria-label="Primary" className="hidden lg:block">
            <ul className="flex items-center gap-1">
              {NAV.map((item) => (
                <li key={item.href}>
                  <Link
                    href={item.href}
                    aria-current={isActive(item.href) ? "page" : undefined}
                    className={cn(
                      "flex flex-col items-center gap-1.5 rounded-sm px-3 pt-3",
                      "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                    )}
                  >
                    <span
                      className={cn(
                        "text-small",
                        isActive(item.href)
                          ? "font-medium text-primary"
                          : "text-foreground-secondary hover:text-foreground"
                      )}
                    >
                      {item.label}
                    </span>
                    <span
                      className={cn(
                        "h-0.5 w-full rounded-full",
                        isActive(item.href) ? "bg-primary" : "bg-transparent"
                      )}
                      aria-hidden="true"
                    />
                  </Link>
                </li>
              ))}
            </ul>
          </nav>
        </div>

        <div className="hidden items-center gap-3 lg:flex">
          <Link
            href="/jobs"
            className="flex h-10 w-64 items-center gap-2 rounded-full bg-muted px-4 text-small text-muted-foreground hover:bg-secondary-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            <Search className="size-4" aria-hidden="true" />
            Search roles
          </Link>
          <Button variant="ghost" asChild>
            <Link href="/sign-in">Sign in</Link>
          </Button>
          <Button asChild>
            <Link href="/register">Create account</Link>
          </Button>
        </div>

        <div className="flex items-center gap-1 lg:hidden">
          <Button variant="ghost" size="icon" aria-label="Search roles" asChild>
            <Link href="/jobs">
              <Search />
            </Link>
          </Button>
          <Button
            variant="ghost"
            size="icon"
            aria-label={open ? "Close menu" : "Open menu"}
            aria-expanded={open}
            aria-controls="mobile-nav"
            onClick={() => setOpen((v) => !v)}
          >
            {open ? <X /> : <Menu />}
          </Button>
        </div>
      </div>

      {open ? (
        <div id="mobile-nav" className="border-t border-border bg-surface lg:hidden">
          <nav aria-label="Primary mobile" className="container-page py-4">
            <ul className="flex flex-col gap-1">
              {NAV.map((item) => (
                <li key={item.href}>
                  <Link
                    href={item.href}
                    onClick={() => setOpen(false)}
                    aria-current={isActive(item.href) ? "page" : undefined}
                    className={cn(
                      "flex h-control-md items-center rounded-md px-3 text-small",
                      isActive(item.href)
                        ? "bg-primary-subtle font-medium text-primary"
                        : "text-foreground-secondary hover:bg-muted"
                    )}
                  >
                    {item.label}
                  </Link>
                </li>
              ))}
            </ul>
            <div className="mt-4 flex flex-col gap-2">
              <Button variant="outline" asChild>
                <Link href="/sign-in" onClick={() => setOpen(false)}>
                  Sign in
                </Link>
              </Button>
              <Button asChild>
                <Link href="/register" onClick={() => setOpen(false)}>
                  Create account
                </Link>
              </Button>
            </div>
          </nav>
        </div>
      ) : null}
    </header>
  );
}
