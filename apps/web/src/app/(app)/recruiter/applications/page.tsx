"use client";

import * as React from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Briefcase, Building2, Clock, FileText, LayoutDashboard, Users } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { DashboardShell, type DashboardNavItem } from "@/components/dashboard/dashboard-shell";
import { ApiError, formError } from "@/lib/auth/client";
import { listRecruiterApplicationsForJob } from "@/lib/applications/api";
import type { RecruiterApplicationSummary } from "@/lib/applications/types";
import { listJobs } from "@/lib/jobs/api";
import type { JobSummary } from "@/lib/jobs/types";
import { formatDate } from "@/lib/utils";
import { ApplicationStatusBadge } from "./_components/application-status-badge";

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
    async (id: string) => {
      setAppsLoading(true);
      setAppsError(undefined);
      setNotFound(false);
      try {
        const token = await getToken();
        setApplications(await listRecruiterApplicationsForJob(token, id));
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
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (jobId) void loadApplications(jobId);
  }, [jobId, loadApplications]);

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
                  <Button type="button" variant="outline" size="sm" onClick={() => void loadApplications(jobId)}>
                    Try again
                  </Button>
                </div>
              </Alert>
            ) : applications.length === 0 ? (
              <EmptyState
                icon={<Users className="size-6" />}
                title="No applications yet"
                description="Check back once candidates start applying to this role."
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
