import type { Metadata } from "next";
import Link from "next/link";
import { SiteFooter } from "@/components/layout/site-footer";
import { Logo } from "@/components/layout/logo";
import { AidxNavLinks } from "@/components/aidx/aidx-nav-links";

export const metadata: Metadata = {
  title: {
    default: "AIDX Lab",
    template: "%s — AIDX Lab",
  },
  description:
    "AIDX Lab: research, projects, people, publications, news, events and research opportunities within StepIn.",
};

const NAV = [
  { href: "/aidx/research", label: "Research" },
  { href: "/aidx/projects", label: "Projects" },
  { href: "/aidx/people", label: "People" },
  { href: "/aidx/publications", label: "Publications" },
  { href: "/aidx/news", label: "News" },
  { href: "/aidx/events", label: "Events" },
  { href: "/aidx/opportunities", label: "Opportunities" },
  { href: "/aidx/about", label: "About" },
  { href: "/aidx/contact", label: "Contact" },
];

/**
 * Shared AIDX shell. Public: no authentication wrapper. The navigation wraps onto as many
 * lines as the viewport needs instead of collapsing into a menu, so every destination stays
 * visible and reachable with the keyboard on every screen size and nothing scrolls sideways.
 */
export default function AidxLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col">
      <a
        href="#main"
        className="sr-only-focusable absolute left-4 top-3 z-50 rounded-md bg-primary px-4 py-2 text-small font-semibold text-primary-foreground"
      >
        Skip to main content
      </a>

      <header className="border-b border-border bg-surface">
        <div className="container-page flex flex-col gap-4 py-4">
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div className="flex items-center gap-4">
              <Link href="/aidx" className="flex items-center gap-3 rounded-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring">
                <span className="text-h4 font-semibold text-foreground">AIDX Lab</span>
              </Link>
              <span aria-hidden="true" className="hidden h-6 w-px bg-border sm:block" />
              <Link href="/" className="hidden text-small text-muted-foreground underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-ring sm:inline">
                Part of StepIn
              </Link>
            </div>
            <Logo />
          </div>

          <nav aria-label="AIDX Lab">
            <AidxNavLinks items={NAV} />
          </nav>
        </div>
      </header>

      <main id="main" className="flex-1">
        {children}
      </main>

      <SiteFooter />
    </div>
  );
}
