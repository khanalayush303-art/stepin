import type { Metadata } from "next";
import { Building2 } from "lucide-react";
import { PageHeader } from "@/components/layout/page-header";
import { CompaniesBrowser } from "@/components/jobs/companies-browser";
import { COMPANIES } from "@/lib/placeholder-data";

export const metadata: Metadata = {
  title: "Verified employers",
  description:
    "Browse employers on StepIn, their industries and their open roles. Verification status is shown on every profile.",
};

export default async function CompaniesPage({
  searchParams,
}: {
  searchParams: Promise<{ state?: string }>;
}) {
  const { state } = await searchParams;

  return (
    <>
      <PageHeader
        crumbs={[{ label: "Home", href: "/" }, { label: "Companies" }]}
        title="Employers on StepIn"
        description="We check every employer's ABN, domain and hiring contact before they can post a role. Verification status is shown on every profile."
      >
        <p className="inline-flex items-center gap-2 text-small text-muted-foreground">
          <Building2 className="size-4" aria-hidden="true" />
          {COMPANIES.filter((c) => c.verified).length} verified of {COMPANIES.length} listed
        </p>
      </PageHeader>

      <CompaniesBrowser companies={COMPANIES} state={state} />
    </>
  );
}
