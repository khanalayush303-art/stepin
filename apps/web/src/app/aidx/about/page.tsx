import type { Metadata } from "next";
import Link from "next/link";
import { PageHeader } from "@/components/layout/page-header";

export const metadata: Metadata = {
  title: "About",
  description: "What AIDX Lab is, how it relates to StepIn, and how to collaborate with it.",
  alternates: { canonical: "/aidx/about" },
};

/**
 * Describes the module's structure. It deliberately makes no claims about institutional
 * affiliations, funding, history or outcomes: those need approved copy before they appear here.
 */
export default function AidxAboutPage() {
  return (
    <>
      <PageHeader crumbs={[{ label: "AIDX Lab", href: "/aidx" }, { label: "About" }]} title="About AIDX Lab" description="AIDX Lab is the research and innovation side of StepIn." />

      <div className="container-page grid max-w-4xl gap-10 py-12">
        <section aria-labelledby="focus-heading" className="space-y-3">
          <h2 id="focus-heading" className="text-h3 text-foreground">
            Research focus
          </h2>
          <p className="text-body-lg text-foreground-secondary">
            The lab works in artificial intelligence and data science. Its work is organised into research areas, and
            each project, publication and event sits within one or more of them.
          </p>
        </section>

        <section aria-labelledby="relationship-heading" className="space-y-3">
          <h2 id="relationship-heading" className="text-h3 text-foreground">
            How it relates to StepIn
          </h2>
          <p className="text-body-lg text-foreground-secondary">
            AIDX Lab shares StepIn&apos;s accounts, platform and job system. Research opportunities are published as StepIn
            roles, so candidates apply through the same StepIn application process as for any other role.
          </p>
        </section>

        <section aria-labelledby="collaboration-heading" className="space-y-3">
          <h2 id="collaboration-heading" className="text-h3 text-foreground">
            Collaboration and opportunities
          </h2>
          <p className="text-body-lg text-foreground-secondary">
            Browse <Link href="/aidx/projects" className="font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring">current projects</Link>,{" "}
            <Link href="/aidx/opportunities" className="font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring">research opportunities</Link>,
            or <Link href="/aidx/contact" className="font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring">get in touch</Link> about collaboration.
          </p>
        </section>
      </div>
    </>
  );
}
