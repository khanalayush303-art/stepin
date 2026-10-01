"use client";

import * as React from "react";
import Link from "next/link";
import { Bell, Bookmark, Briefcase, FileText, GraduationCap, LayoutDashboard, MapPin, Search, User } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { DashboardShell, type DashboardNavItem } from "@/components/dashboard/dashboard-shell";
import { SaveJobButton } from "@/components/jobs/save-job-button";
import { formError } from "@/lib/auth/client";
import { listSavedJobs } from "@/lib/savedJobs/api";
import type { SavedJob } from "@/lib/savedJobs/types";
import { formatDate } from "@/lib/utils";

const NAV: DashboardNavItem[] = [
  { href: "/dashboard", label: "Overview", icon: LayoutDashboard },
  { href: "/dashboard/discover", label: "Discover roles", icon: Search },
  { href: "/dashboard/applications", label: "Applications", icon: FileText, badge: "12" },
  { href: "/dashboard/saved", label: "Saved roles", icon: Bookmark, badge: "8" },
  { href: "/dashboard/interviews", label: "Interviews", icon: User, badge: "2" },
  { href: "/dashboard/prepare", label: "Prepare", icon: GraduationCap },
  { href: "/dashboard/notifications", label: "Notifications", icon: Bell, badge: "3" },
];

export default function SavedJobsPage() {
  const { getToken } = useAuth();
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [savedJobs, setSavedJobs] = React.useState<SavedJob[]>([]);

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      setSavedJobs(await listSavedJobs(token));
    } catch (error) {
      setLoadError(formError(error));
    } finally {
      setLoading(false);
    }
  }, [getToken]);

  React.useEffect(() => {
    // Fetch-on-mount: same documented pattern as every other dashboard list page.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  function onUnsaved(jobId: string) {
    setSavedJobs((current) => current.filter((s) => s.jobId !== jobId));
  }

  return (
    <DashboardShell nav={NAV} navLabel="Applicant dashboard">
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Saved roles</h1>
          <p className="text-body text-muted-foreground">Roles you&rsquo;ve bookmarked to come back to later.</p>
        </div>

        {loading ? (
          <div className="space-y-3">
            <Skeleton className="h-16 w-full" />
            <Skeleton className="h-16 w-full" />
            <Skeleton className="h-16 w-full" />
          </div>
        ) : loadError ? (
          <Alert tone="error" title="Couldn't load your saved roles">
            {loadError}
            <div className="mt-3">
              <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
                Try again
              </Button>
            </div>
          </Alert>
        ) : savedJobs.length === 0 ? (
          <EmptyState
            icon={<Bookmark className="size-6" />}
            title="No saved roles yet"
            description="Bookmark a role while browsing to keep track of it here."
            action={
              <Button asChild>
                <Link href="/dashboard/discover">Discover roles</Link>
              </Button>
            }
          />
        ) : (
          <Card className="p-6">
            <ul className="divide-y divide-border">
              {savedJobs.map((savedJob) => {
                const isAvailable = savedJob.jobStatus === "Published";
                return (
                  <li key={savedJob.id} className="flex flex-wrap items-center gap-4 py-4 first:pt-0 last:pb-0">
                    <div className="min-w-0 flex-1 space-y-1">
                      <div className="flex flex-wrap items-center gap-2">
                        {isAvailable ? (
                          <Link
                            href={`/dashboard/discover/${savedJob.jobId}`}
                            className="rounded-sm text-small font-medium text-foreground hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                          >
                            {savedJob.jobTitle}
                          </Link>
                        ) : (
                          <span className="text-small font-medium text-foreground">{savedJob.jobTitle}</span>
                        )}
                        {!isAvailable ? (
                          <Badge tone="neutral" variant="subtle">
                            No longer available
                          </Badge>
                        ) : null}
                      </div>
                      <p className="flex flex-wrap items-center gap-x-3 gap-y-1 text-caption text-muted-foreground">
                        <span>{savedJob.companyName}</span>
                        <span className="flex items-center gap-1">
                          <MapPin className="size-3.5 shrink-0" aria-hidden="true" />
                          {savedJob.location}
                        </span>
                        <span className="flex items-center gap-1">
                          <Briefcase className="size-3.5 shrink-0" aria-hidden="true" />
                          {savedJob.employmentType}
                        </span>
                        <span>Saved {formatDate(savedJob.savedAt)}</span>
                      </p>
                    </div>
                    <SaveJobButton
                      jobId={savedJob.jobId}
                      saved
                      onSavedChange={(stillSaved) => {
                        if (!stillSaved) onUnsaved(savedJob.jobId);
                      }}
                    />
                    {isAvailable ? (
                      <Button variant="outline" size="sm" asChild>
                        <Link href={`/dashboard/discover/${savedJob.jobId}`}>View role</Link>
                      </Button>
                    ) : null}
                  </li>
                );
              })}
            </ul>
          </Card>
        )}
      </div>
    </DashboardShell>
  );
}
