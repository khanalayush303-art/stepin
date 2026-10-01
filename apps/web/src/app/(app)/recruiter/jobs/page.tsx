"use client";

import * as React from "react";
import Link from "next/link";
import { Briefcase, Building2, Clock, FileText, LayoutDashboard, Plus, Users } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { DashboardShell, type DashboardNavItem } from "@/components/dashboard/dashboard-shell";
import { formError } from "@/lib/auth/client";
import { listJobs, publishJob, unpublishJob } from "@/lib/jobs/api";
import type { JobSummary } from "@/lib/jobs/types";
import { formatDate } from "@/lib/utils";
import { JobStatusBadge } from "./_components/job-status-badge";

const NAV: DashboardNavItem[] = [
  { href: "/recruiter", label: "Overview", icon: LayoutDashboard },
  { href: "/recruiter/jobs", label: "Job listings", icon: FileText },
  { href: "/recruiter/applications", label: "Applications", icon: Users },
  { href: "/recruiter/interviews", label: "Interviews", icon: Clock, badge: "5" },
  { href: "/recruiter/profile", label: "Company profile", icon: Building2 },
];

export default function MyJobsPage() {
  const { getToken } = useAuth();
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [jobs, setJobs] = React.useState<JobSummary[]>([]);
  const [pendingId, setPendingId] = React.useState<string>();
  const [actionError, setActionError] = React.useState<string>();

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      setJobs(await listJobs(token));
    } catch (error) {
      setLoadError(formError(error));
    } finally {
      setLoading(false);
    }
  }, [getToken]);

  React.useEffect(() => {
    // Fetch-on-mount: same documented pattern as the profile pages.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  async function onTogglePublish(job: JobSummary) {
    setPendingId(job.id);
    setActionError(undefined);
    try {
      const token = await getToken();
      if (job.status === "Published") {
        await unpublishJob(token, job.id);
      } else {
        await publishJob(token, job.id);
      }
      await load();
    } catch (error) {
      setActionError(formError(error));
    } finally {
      setPendingId(undefined);
    }
  }

  return (
    <DashboardShell nav={NAV} navLabel="Recruiter dashboard">
      <div className="space-y-6">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="space-y-1">
            <h1 className="text-h2 text-foreground">My jobs</h1>
            <p className="text-body text-muted-foreground">
              Create, edit and publish the roles you&rsquo;re hiring for.
            </p>
          </div>
          <Button asChild>
            <Link href="/recruiter/jobs/new">
              <Plus />
              Post a role
            </Link>
          </Button>
        </div>

        {actionError ? (
          <Alert tone="error" title="Couldn't update that job">
            {actionError}
          </Alert>
        ) : null}

        {loading ? (
          <div className="space-y-3">
            <Skeleton className="h-16 w-full" />
            <Skeleton className="h-16 w-full" />
            <Skeleton className="h-16 w-full" />
          </div>
        ) : loadError ? (
          <Alert tone="error" title="Couldn't load your jobs">
            {loadError}
            <div className="mt-3">
              <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
                Try again
              </Button>
            </div>
          </Alert>
        ) : jobs.length === 0 ? (
          <EmptyState
            icon={<Briefcase className="size-6" />}
            title="No jobs yet"
            description="Post your first role to start building your candidate pipeline."
            action={
              <Button asChild>
                <Link href="/recruiter/jobs/new">
                  <Plus />
                  Post a role
                </Link>
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
                      href={`/recruiter/jobs/${job.id}/edit`}
                      className="rounded-sm text-small font-medium text-foreground hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                    >
                      {job.title}
                    </Link>
                    <p className="text-caption text-muted-foreground">
                      {job.companyName} &middot; created {formatDate(job.createdAt)}
                      {job.updatedAt ? <> &middot; updated {formatDate(job.updatedAt)}</> : null}
                    </p>
                  </div>
                  <JobStatusBadge status={job.status} />
                  <div className="flex items-center gap-2">
                    <Button variant="outline" size="sm" asChild>
                      <Link href={`/recruiter/applications?jobId=${job.id}`}>Applications</Link>
                    </Button>
                    <Button variant="outline" size="sm" asChild>
                      <Link href={`/recruiter/jobs/${job.id}/edit`}>Edit</Link>
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      loading={pendingId === job.id}
                      onClick={() => void onTogglePublish(job)}
                    >
                      {job.status === "Published" ? "Unpublish" : "Publish"}
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          </Card>
        )}
      </div>
    </DashboardShell>
  );
}
