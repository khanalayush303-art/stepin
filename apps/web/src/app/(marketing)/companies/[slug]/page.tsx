import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { Building2 } from "lucide-react";
import { Card } from "@/components/ui/card";
import { PageHeader } from "@/components/layout/page-header";
import { JobCard } from "@/components/jobs/job-card";
import { VerifiedBadge } from "@/components/jobs/verified-badge";
import { COMPANIES, JOBS } from "@/lib/placeholder-data";

export function generateStaticParams() {
  return COMPANIES.map((company) => ({ slug: company.slug }));
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  const company = COMPANIES.find((c) => c.slug === slug);
  return company ? { title: company.name } : {};
}

export default async function CompanyDetailPage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = await params;
  const company = COMPANIES.find((c) => c.slug === slug);
  if (!company) notFound();

  const roles = JOBS.filter((job) => job.company.slug === company.slug);

  return (
    <>
      <PageHeader
        crumbs={[
          { label: "Home", href: "/" },
          { label: "Companies", href: "/companies" },
          { label: company.name },
        ]}
        title={company.name}
        description={company.summary}
      >
        <div className="flex flex-wrap items-center gap-3">
          <VerifiedBadge verified={company.verified} label="Verified employer" />
          <span className="text-small text-muted-foreground">
            {company.industry} &middot; {company.size} &middot; {company.location}
          </span>
        </div>
      </PageHeader>

      <section className="container-page space-y-6 py-10 lg:py-12">
        <h2 className="text-h3 text-foreground">
          Open roles at {company.name} ({roles.length})
        </h2>

        {roles.length === 0 ? (
          <Card className="flex flex-col items-center gap-3 p-12 text-center">
            <Building2 className="size-8 text-muted-foreground" aria-hidden="true" />
            <p className="text-h4 text-foreground">No roles open right now</p>
            <p className="text-small text-muted-foreground">
              {company.name} has hired through StepIn before. Follow them to hear when the
              next role opens.
            </p>
          </Card>
        ) : (
          <ul className="grid gap-6 md:grid-cols-2">
            {roles.map((job) => (
              <li key={job.id} className="relative flex">
                <JobCard job={job} />
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  );
}
