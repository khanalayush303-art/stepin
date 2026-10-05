import type { Metadata } from "next";
import Link from "next/link";
import { PageHeader } from "@/components/layout/page-header";
import { Alert } from "@/components/ui/alert";

export const metadata: Metadata = {
  title: "Contact",
  description: "How to contact AIDX Lab about research, collaboration and opportunities.",
  alternates: { canonical: "/aidx/contact" },
};

/**
 * No contact form and no contact address yet. Neither exists in the approved project content,
 * so this page says so plainly rather than inventing an address or a form that does not submit.
 */
export default function AidxContactPage() {
  return (
    <>
      <PageHeader crumbs={[{ label: "AIDX Lab", href: "/aidx" }, { label: "Contact" }]} title="Contact AIDX Lab" description="For research enquiries and collaboration." />

      <div className="container-page grid max-w-3xl gap-8 py-12">
        <Alert tone="info" title="Contact details are being confirmed">
          The lab&apos;s official contact address will be published here once it is confirmed. Until then, the routes
          below answer most questions.
        </Alert>

        <section aria-labelledby="ways-heading" className="space-y-3">
          <h2 id="ways-heading" className="text-h3 text-foreground">
            Ways to reach us
          </h2>
          <ul className="list-disc space-y-2 pl-5 text-body-lg text-foreground-secondary">
            <li>
              Questions about a research role: open the role from the{" "}
              <Link href="/aidx/opportunities" className="font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring">
                opportunities page
              </Link>
              .
            </li>
            <li>
              Questions about the platform or your account: see the{" "}
              <Link href="/how-it-works" className="font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring">
                StepIn how-it-works page
              </Link>
              .
            </li>
          </ul>
        </section>
      </div>
    </>
  );
}
