import type { Metadata } from "next";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { PageHeader } from "@/components/layout/page-header";

export const metadata: Metadata = {
  title: "About",
  description:
    "Why StepIn exists, the principles behind the product, and how to get in touch.",
};

const PRINCIPLES = [
  {
    title: "No black box",
    body: "If an application has a state, the applicant can see it. Silence is a product failure, not a neutral outcome.",
  },
  {
    title: "Verified before visible",
    body: "An employer is checked before they can post. A candidate should never have to research whether a listing is real.",
  },
  {
    title: "Accessible by default",
    body: "WCAG 2.1 AA is the floor, not the goal. Status is never colour alone, focus is never removed, and every control is reachable by keyboard.",
  },
  {
    title: "Built for the start of a career",
    body: "The product assumes this is someone's first professional application, and explains the process rather than assuming it.",
  },
];

export default function AboutPage() {
  return (
    <>
      <PageHeader
        crumbs={[{ label: "Home", href: "/" }, { label: "About" }]}
        title="About StepIn"
        description="A recruitment platform for the start of a career, built around one idea: an applicant should always know where they stand."
      />

      <section className="container-page grid gap-10 py-12 lg:grid-cols-[minmax(0,1fr)_320px] lg:py-16">
        <div className="space-y-5">
          <h2 className="text-h3 text-foreground">Why this exists</h2>
          <p className="text-body text-muted-foreground">
            Most job boards are optimised for volume. A student applying for their first
            role sends dozens of applications into silence, with no way to tell a real
            listing from a stale one, or a process that has moved on from one that never
            started.
          </p>
          <p className="text-body text-muted-foreground">
            StepIn narrows the problem deliberately. Fewer employers, checked before they
            appear. One profile that gets reused. A visible stage for every application,
            updated by the employer rather than guessed at by the applicant. The product
            does less than a general job board on purpose, so the part that matters works
            properly.
          </p>

          <h2 className="pt-4 text-h3 text-foreground">Principles</h2>
          <ul className="grid gap-4 sm:grid-cols-2">
            {PRINCIPLES.map((item) => (
              <li key={item.title}>
                <Card className="h-full p-5">
                  <h3 className="text-h4 text-foreground">{item.title}</h3>
                  <p className="mt-1.5 text-small text-muted-foreground">{item.body}</p>
                </Card>
              </li>
            ))}
          </ul>
        </div>

        <aside className="space-y-4">
          <Card className="space-y-3 p-6">
            <h2 className="text-h4 text-foreground">Product status</h2>
            <dl className="space-y-2.5 text-small">
              <div className="flex justify-between gap-3">
                <dt className="text-muted-foreground">Phase</dt>
                <dd className="font-medium text-foreground">0 — Foundation</dd>
              </div>
              <div className="flex justify-between gap-3">
                <dt className="text-muted-foreground">Accessibility target</dt>
                <dd className="font-medium text-foreground">WCAG 2.1 AA</dd>
              </div>
              <div className="flex justify-between gap-3">
                <dt className="text-muted-foreground">Based in</dt>
                <dd className="font-medium text-foreground">Sydney, Australia</dd>
              </div>
            </dl>
            <p className="text-caption text-muted-foreground">
              Accounts, applications and employer tooling arrive in later phases. What you
              can see today is the interface and the design system behind it.
            </p>
          </Card>

          <Card className="space-y-3 p-6">
            <h2 className="text-h4 text-foreground">Get in touch</h2>
            <p className="text-small text-muted-foreground">
              Questions about verification, accessibility, or listing your organisation.
            </p>
            <Button variant="outline" asChild className="w-full">
              <Link href="/how-it-works">Read how it works</Link>
            </Button>
          </Card>
        </aside>
      </section>
    </>
  );
}
