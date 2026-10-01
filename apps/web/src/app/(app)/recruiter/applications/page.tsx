"use client";

import * as React from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Briefcase, Building2, Clock, FileText, LayoutDashboard, Search, Users } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { DashboardShell, type DashboardNavItem } from "@/components/dashboard/dashboard-shell";
import { ApiError, formError } from "@/lib/auth/client";
import { listRecruiterApplicationsForJob } from "@/lib/applications/api";
import { APPLICATION_STATUSES, type ApplicationSort, type ApplicationStatus, type RecruiterApplicationFilters, type RecruiterApplicationSummary } from "@/lib/applications/types";
import { listJobs } from "@/lib/jobs/api";
import type { JobSummary } from "@/lib/jobs/types";
import { formatDate } from "@/lib/utils";
import { ApplicationStatusBadge } from "@/components/applications/application-status-badge";

/** Radix Select can't carry an empty-string item value, so "no status filter" gets its own sentinel — same convention as dashboard/discover/page.tsx. */
const ANY_STATUS = "any";

const EMPTY_FILTERS: RecruiterApplicationFilters = {};

const NAV: DashboardNavItem[] = [
  { href: "/recruiter", label: "Overview", icon: LayoutDashboard },
  { href: "/recruiter/jobs", label: "Job listings", icon: FileText },
  { href: "/recruiter/applications", label: "Applications", icon: Users },
  { href: "/recruiter/interviews", label: "Interviews", icon: Clock, badge: "5" },
  { href: "/recruiter/profile", label: "Company profile", icon: Building2 },
];

function RecruiterApplicationsView() {
  const searchParams = useSearchParams();
  const jobId = searchParams.get("jobId");
  const { getToken } = useAuth();

  const [jobsLoading, setJobsLoading] = React.useState(true);
  const [jobsError, setJobsError] = React.useState<string>();
  const [jobs, setJobs] = React.useState<JobSummary[]>([]);

  const [appsLoading, setAppsLoading] = React.useState(false);
  const [appsError, setAppsError] = React.useState<string>();
  const [notFound, setNotFound] = React.useState(false);
  const [applications, setApplications] = React.useState<RecruiterApplicationSummary[]>([]);

  const [appliedFilters, setAppliedFilters] = React.useState<RecruiterApplicationFilters>(EMPTY_FILTERS);
  const [statusDraft, setStatusDraft] = React.useState<ApplicationStatus | typeof ANY_STATUS>(ANY_STATUS);
  const [searchDraft, setSearchDraft] = React.useState("");
  const [sortDraft, setSortDraft] = React.useState<ApplicationSort>("newest");

  const loadJobs = React.useCallback(async () => {
    setJobsLoading(true);
    setJobsError(undefined);
    try {
      const token = await getToken();
      setJobs(await listJobs(token));
    } catch (error) {
      setJobsError(formError(error));
    } finally {
      setJobsLoading(false);
    }
  }, [getToken]);

  const loadApplications = React.useCallback(
    async (id: string, filters: RecruiterApplicationFilters) => {
      setAppsLoading(true);
      setAppsError(undefined);
      setNotFound(false);
      try {
        const token = await getToken();
        setApplications(await listRecruiterApplicationsForJob(token, id, filters));
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) {
          setNotFound(true);
        } else {
          setAppsError(formError(error));
        }
      } finally {
        setAppsLoading(false);
      }
    },
    [getToken]
  );

  React.useEffect(() => {
    // Fetch-on-mount: same documented pattern as every other dashboard page.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void loadJobs();
  }, [loadJobs]);

  React.useEffect(() => {
    // Reset any filters left over from a previously-viewed job — a filter
    // chosen for one job's applications shouldn't silently carry over to the next.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setStatusDraft(ANY_STATUS);
    setSearchDraft("");
    setSortDraft("newest");
    setAppliedFilters(EMPTY_FILTERS);
  }, [jobId]);

  React.useEffect(() => {
    // Fetch-on-mount, and again whenever appliedFilters changes (Search
    // button/Enter, not live-as-you-type): same documented pattern as
    // dashboard/discover/page.tsx's job search.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (jobId) void loadApplications(jobId, appliedFilters);
  }, [jobId, appliedFilters, loadApplications]);

  function onFilterSubmit(e: React.FormEvent) {
    e.preventDefault();
    setAppliedFilters({
      status: statusDraft === ANY_STATUS ? undefined : statusDraft,
      search: searchDraft.trim() || undefined,
      sort: sortDraft,
    });
  }

  function onFilterClear() {
    setStatusDraft(ANY_STATUS);
    setSearchDraft("");
    setSortDraft("newest");
    setAppliedFilters(EMPTY_FILTERS);
  }

  const hasActiveFilters = Boolean(appliedFilters.status || appliedFilters.search || (appliedFilters.sort && appliedFilters.sort !== "newest"));

  const selectedJob = jobs.find((job) => job.id === jobId);

  return (
    <DashboardShell nav={NAV} navLabel="Recruiter dashboard">
      <div className="space-y-6">
        {!jobId ? (
          <>
            <div className="space-y-1">
              <h1 className="text-h2 text-foreground">Applications</h1>
              <p className="text-body text-muted-foreground">Pick a job to see who&rsquo;s applied.</p>
            </div>

            {jobsLoading ? (
              <div className="space-y-3">
                <Skeleton className="h-16 w-full" />
                <Skeleton className="h-16 w-full" />
              </div>
            ) : jobsError ? (
              <Alert tone="error" title="Couldn't load your jobs">
                {jobsError}
                <div className="mt-3">
                  <Button type="button" variant="outline" size="sm" onClick={() => void loadJobs()}>
                    Try again
                  </Button>
                </div>
              </Alert>
            ) : jobs.length === 0 ? (
              <EmptyState
                icon={<Briefcase className="size-6" />}
                title="No jobs yet"
                description="Post a role before you can review applications for it."
                action={
                  <Button asChild>
                    <Link href="/recruiter/jobs/new">Post a role</Link>
                  </Button>
                }
              />
            ) : (
              <Card className="p-6">
                <ul className="divide-y divide-border">
                  {jobs.map((job) => (
                    <li key={job.id} className="flex flex-wrap items-center gap-4 py-4 first:pt-0 last:pb-0">
                      <div className="min-w-0 flex-1 space-y-1">
                        <Link
                          href={`/recruiter/applications?jobId=${job.id}`}
                          className="rounded-sm text-small font-medium text-foreground hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                        >
                          {job.title}
                        </Link>
                        <p className="text-caption text-muted-foreground">{job.companyName}</p>
                      </div>
                      <Button variant="outline" size="sm" asChild>
                        <Link href={`/recruiter/applications?jobId=${job.id}`}>View applications</Link>
                      </Button>
                    </li>
                  ))}
                </ul>
              </Card>
            )}
          </>
        ) : (
          <>
            <Link
              href="/recruiter/applications"
              className="text-small font-medium text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
            >
              &larr; All jobs
            </Link>

            <div className="space-y-1">
              <h1 className="text-h2 text-foreground">{selectedJob?.title ?? "Applications"}</h1>
              <p className="text-body text-muted-foreground">Applications submitted to this role.</p>
            </div>

            {notFound ? null : (
              <Card className="p-4">
                <form onSubmit={onFilterSubmit} className="flex flex-wrap items-end gap-3">
                  <div className="min-w-[200px] flex-1">
                    <label htmlFor="candidate-search" className="sr-only">
                      Search candidates
                    </label>
                    <Input
                      id="candidate-search"
                      value={searchDraft}
                      onChange={(e) => setSearchDraft(e.target.value)}
                      placeholder="Search candidates..."
                    />
                  </div>
                  <Select value={statusDraft} onValueChange={(v) => setStatusDraft(v as ApplicationStatus | typeof ANY_STATUS)}>
                    <SelectTrigger className="w-[160px]">
                      <SelectValue placeholder="Status" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={ANY_STATUS}>All statuses</SelectItem>
                      {APPLICATION_STATUSES.map((value) => (
                        <SelectItem key={value} value={value}>
                          {value}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <Select value={sortDraft} onValueChange={(v) => setSortDraft(v as ApplicationSort)}>
                    <SelectTrigger className="w-[140px]">
                      <SelectValue placeholder="Sort" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="newest">Newest</SelectItem>
                      <SelectItem value="oldest">Oldest</SelectItem>
                    </SelectContent>
                  </Select>
                  <Button type="submit">
                    <Search />
                    Apply
                  </Button>
                  {hasActiveFilters ? (
                    <Button type="button" variant="ghost" onClick={onFilterClear}>
                      Clear
                    </Button>
                  ) : null}
                </form>
              </Card>
            )}

            {appsLoading ? (
              <div className="space-y-3">
                <Skeleton className="h-16 w-full" />
                <Skeleton className="h-16 w-full" />
              </div>
            ) : notFound ? (
              <Alert tone="error" title="Job not found">
                This job doesn&rsquo;t exist or isn&rsquo;t yours.
              </Alert>
            ) : appsError ? (
              <Alert tone="error" title="Couldn't load applications">
                {appsError}
                <div className="mt-3">
                  <Button type="button" variant="outline" size="sm" onClick={() => void loadApplications(jobId, appliedFilters)}>
                    Try again
                  </Button>
                </div>
              </Alert>
            ) : applications.length === 0 ? (
              <EmptyState
                icon={<Users className="size-6" />}
                title={hasActiveFilters ? "No applications match these filters" : "No applications yet"}
                description={
                  hasActiveFilters
                    ? "Try a different status, search term, or clear your filters."
                    : "Check back once candidates start applying to this role."
                }
                action={
                  hasActiveFilters ? (
                    <Button type="button" variant="outline" onClick={onFilterClear}>
                      Clear filters
                    </Button>
                  ) : undefined
                }
              />
            ) : (
              <Card className="p-6">
                <ul className="divide-y divide-border">
                  {applications.map((application) => (
                    <li key={application.id} className="flex flex-wrap items-center gap-4 py-4 first:pt-0 last:pb-0">
                      <div className="min-w-0 flex-1 space-y-1">
                        <Link
                          href={`/recruiter/applications/${application.id}`}
                          className="rounded-sm text-small font-medium text-foreground hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                        >
                          {application.applicantName}
                        </Link>
                        <p className="text-caption text-muted-foreground">
                          {application.applicantEmail} &middot; applied {formatDate(application.createdAt)}
                        </p>
                      </div>
                      <ApplicationStatusBadge status={application.status} />
                      <Button variant="outline" size="sm" asChild>
                        <Link href={`/recruiter/applications/${application.id}`}>View</Link>
                      </Button>
                    </li>
                  ))}
                </ul>
              </Card>
            )}
          </>
        )}
      </div>
    </DashboardShell>
  );
}

export default function RecruiterApplicationsPage() {
  return (
    <React.Suspense fallback={null}>
      <RecruiterApplicationsView />
    </React.Suspense>
  );
}
