import type { Metadata } from "next";
import { PageHeader } from "@/components/layout/page-header";
import { JobSearchRow } from "@/components/jobs/job-search-row";
import { JobsBrowser, type BrowseState } from "@/components/jobs/jobs-browser";
import { buildJobFilters } from "@/lib/filters";
import { JOBS } from "@/lib/placeholder-data";

export const metadata: Metadata = {
  title: "Graduate & entry-level jobs",
  description:
    "Browse graduate and entry-level roles from verified employers across Australia.",
};

const STATES: BrowseState[] = ["ready", "loading", "empty", "error"];

export default async function JobsPage({
  searchParams,
}: {
  searchParams: Promise<{ state?: string }>;
}) {
  const { state } = await searchParams;
  const jobs = JOBS.filter((job) => !job.isInternship);
  const view = STATES.includes(state as BrowseState) ? (state as BrowseState) : "ready";

  return (
    <>
      <PageHeader
        crumbs={[{ label: "Home", href: "/" }, { label: "Jobs" }]}
        title="Graduate & entry-level jobs"
        description="471 live roles from 128 verified employers across Australia."
      >
        <JobSearchRow />
      </PageHeader>

      <JobsBrowser jobs={jobs} groups={buildJobFilters(jobs)} state={view} />
    </>
  );
}
