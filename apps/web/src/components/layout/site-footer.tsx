import Link from "next/link";
import { Logo } from "./logo";

const GROUPS = [
  {
    title: "For applicants",
    links: [
      { label: "Browse jobs", href: "/jobs" },
      { label: "Internships", href: "/internships" },
      { label: "Build your profile", href: "/dashboard" },
      { label: "Application tracker", href: "/dashboard" },
      { label: "Interview prep", href: "/how-it-works" },
    ],
  },
  {
    title: "For employers",
    links: [
      { label: "Post a role", href: "/recruiter" },
      { label: "Verification", href: "/how-it-works" },
      { label: "Talent search", href: "/recruiter" },
      { label: "Pricing", href: "/about" },
    ],
  },
  {
    title: "Platform",
    links: [
      { label: "How it works", href: "/how-it-works" },
      { label: "About", href: "/about" },
      { label: "Accessibility", href: "/about" },
      { label: "Privacy", href: "/about" },
      { label: "Contact", href: "/about" },
    ],
  },
];

export function SiteFooter() {
  return (
    <footer className="border-t border-border bg-surface">
      <div className="container-page py-12">
        <div className="flex flex-col gap-10 lg:flex-row lg:gap-16">
          <div className="max-w-xs space-y-3">
            <Logo />
            <p className="text-small text-muted-foreground">
              Verified employers, transparent stages, and a clear view of where every
              application stands.
            </p>
          </div>

          <div className="grid flex-1 grid-cols-2 gap-8 sm:grid-cols-3">
            {GROUPS.map((group) => (
              <div key={group.title}>
                <h2 className="text-small font-medium text-foreground">{group.title}</h2>
                <ul className="mt-3 space-y-2.5">
                  {group.links.map((link) => (
                    <li key={link.label}>
                      <Link
                        href={link.href}
                        className="text-small text-muted-foreground hover:text-foreground hover:underline"
                      >
                        {link.label}
                      </Link>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        </div>

        <div className="mt-10 flex flex-col gap-2 border-t border-border pt-6 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-caption text-muted-foreground">
            &copy; {new Date().getFullYear()} StepIn. All rights reserved.
          </p>
          <p className="text-caption text-muted-foreground">
            WCAG 2.1 AA &middot; Built in Sydney
          </p>
        </div>
      </div>
    </footer>
  );
}
