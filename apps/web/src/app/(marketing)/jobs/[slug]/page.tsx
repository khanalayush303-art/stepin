import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { Bookmark, Briefcase, Building2, Clock, MapPin } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { PageHeader } from "@/components/layout/page-header";
import { VerifiedBadge } from "@/components/jobs/verified-badge";
import { JOBS } from "@/lib/placeholder-data";
import { daysUntil, formatDate } from "@/lib/utils";

export function generateStaticParams() {
  return JOBS.map((job) => ({ slug: job.slug }));
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  const job = JOBS.find((j) => j.slug === slug);
  return job ? { title: `${job.title} at ${job.company.name}` } : {};
}

export default async function JobDetailPage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = await params;
  const job = JOBS.find((j) => j.slug === slug);
  if (!job) notFound();

  const closingIn = daysUntil(job.closesAt);

  return (
    <>
      <PageHeader
        crumbs={[
          { label: "Home", href: "/" },
          { label: job.isInternship ? "Internships" : "Jobs", href: job.isInternship ? "/internships" : "/jobs" },
          { label: job.title },
        ]}
        title={job.title}
      >
        <div className="flex flex-wrap items-center gap-3">
          <span className="inline-flex items-center gap-2 text-small font-medium text-foreground-secondary">
            <Building2 className="size-4" aria-hidden="true" />
            <Link href={`/companies/${job.company.slug}`} className="hover:underline">
              {job.company.name}
            </Link>
          </span>
          <VerifiedBadge verified={job.company.verified} />
        </div>
      </PageHeader>

      <div className="container-page grid gap-8 py-10 lg:grid-cols-[minmax(0,1fr)_320px] lg:py-12">
        <article className="space-y-6">
          <ul className="flex flex-wrap gap-x-6 gap-y-2 text-small text-muted-foreground">
            <li className="flex items-center gap-1.5">
              <MapPin className="size-4" aria-hidden="true" />
              {job.location} &middot; {job.workMode}
            </li>
            <li className="flex items-center gap-1.5">
              <Briefcase className="size-4" aria-hidden="true" />
              {job.workType} &middot; {job.experience}
            </li>
            <li className="flex items-center gap-1.5">
              <Clock className="size-4" aria-hidden="true" />
              Closes {formatDate(job.closesAt)} ({closingIn} days)
            </li>
          </ul>

          <section className="space-y-2">
            <h2 className="text-h3 text-foreground">About the role</h2>
            <p className="text-body text-muted-foreground">{job.summary}</p>
          </section>

          <section className="space-y-2">
            <h2 className="text-h3 text-foreground">What you will need</h2>
            <ul className="list-inside list-disc space-y-1.5 text-body text-muted-foreground">
              {job.skills.map((skill) => (
                <li key={skill}>{skill}</li>
              ))}
            </ul>
          </section>

          {job.internship ? (
            <section className="space-y-2">
              <h2 className="text-h3 text-foreground">Placement details</h2>
              <dl className="grid gap-4 sm:grid-cols-2">
                <div className="rounded-md bg-muted p-4">
                  <dt className="text-caption text-muted-foreground">Duration</dt>
                  <dd className="text-small font-medium text-foreground">
                    {job.internship.durationWeeks} weeks
                  </dd>
                </div>
                <div className="rounded-md bg-muted p-4">
                  <dt className="text-caption text-muted-foreground">Cohort starts</dt>
                  <dd className="text-small font-medium text-foreground">
                    {formatDate(job.internship.cohortStart)}
                  </dd>
                </div>
              </dl>
            </section>
          ) : null}
        </article>

        <aside className="space-y-4 lg:sticky lg:top-24 lg:self-start">
          <Card className="space-y-4 p-6">
            <div>
              <p className="text-h4 text-foreground">{job.compensation}</p>
              <p className="text-caption text-muted-foreground">
                Posted {formatDate(job.postedAt)}
              </p>
            </div>
            <Button size="lg" className="w-full" disabled>
              Apply now
            </Button>
            <p className="text-caption text-muted-foreground">
              Applying opens in a later phase. Nothing is submitted from this preview.
            </p>
            <Button variant="outline" className="w-full">
              <Bookmark />
              Save role
            </Button>
          </Card>
        </aside>
      </div>
    </>
  );
}
