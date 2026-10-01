"use client";

import * as React from "react";
import Link from "next/link";
import { Bell, Bookmark, FileText, GraduationCap, LayoutDashboard, Search, User } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { ApplicationStatusBadge } from "@/components/applications/application-status-badge";
import { DashboardShell, type DashboardNavItem } from "@/components/dashboard/dashboard-shell";
import { formError } from "@/lib/auth/client";
import { listApplications } from "@/lib/applications/api";
import type { ApplicationSummary } from "@/lib/applications/types";
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

export default function MyApplicationsPage() {
  const { getToken } = useAuth();
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [applications, setApplications] = React.useState<ApplicationSummary[]>([]);

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      setApplications(await listApplications(token));
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

  return (
    <DashboardShell nav={NAV} navLabel="Applicant dashboard">
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">My applications</h1>
          <p className="text-body text-muted-foreground">Track the roles you&rsquo;ve applied to.</p>
        </div>

        {loading ? (
          <div className="space-y-3">
            <Skeleton className="h-16 w-full" />
            <Skeleton className="h-16 w-full" />
            <Skeleton className="h-16 w-full" />
          </div>
        ) : loadError ? (
          <Alert tone="error" title="Couldn't load your applications">
            {loadError}
            <div className="mt-3">
              <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
                Try again
              </Button>
            </div>
          </Alert>
        ) : applications.length === 0 ? (
          <EmptyState
            icon={<FileText className="size-6" />}
            title="No applications yet"
            description="You haven't submitted any applications yet."
            action={
              <Button asChild>
                <Link href="/dashboard/discover">Discover roles</Link>
              </Button>
            }
          />
        ) : (
          <Card className="p-6">
            <ul className="divide-y divide-border">
              {applications.map((application) => (
                <li key={application.id} className="flex flex-wrap items-center gap-4 py-4 first:pt-0 last:pb-0">
                  <div className="min-w-0 flex-1 space-y-1">
                    <Link
                      href={`/dashboard/applications/${application.id}`}
                      className="rounded-sm text-small font-medium text-foreground hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                    >
                      {application.jobTitle}
                    </Link>
                    <p className="text-caption text-muted-foreground">
                      {application.companyName} &middot; applied {formatDate(application.createdAt)}
                    </p>
                  </div>
                  <ApplicationStatusBadge status={application.status} />
                  <Button variant="outline" size="sm" asChild>
                    <Link href={`/dashboard/applications/${application.id}`}>View</Link>
                  </Button>
                </li>
              ))}
            </ul>
          </Card>
        )}
      </div>
    </DashboardShell>
  );
}
