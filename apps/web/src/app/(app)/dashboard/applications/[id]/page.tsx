"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useSearchParams } from "next/navigation";
import { Bell, Bookmark, Building2, Download, FileText, GraduationCap, LayoutDashboard, Search, User } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { DashboardShell, type DashboardNavItem } from "@/components/dashboard/dashboard-shell";
import { ApiError, formError } from "@/lib/auth/client";
import { downloadResume, getApplication } from "@/lib/applications/api";
import type { Application } from "@/lib/applications/types";
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

function ApplicationDetailsView() {
  const params = useParams<{ id: string }>();
  const searchParams = useSearchParams();
  const { getToken } = useAuth();

  const [application, setApplication] = React.useState<Application | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [notFound, setNotFound] = React.useState(false);
  const [downloading, setDownloading] = React.useState(false);
  const [downloadError, setDownloadError] = React.useState<string>();

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    setNotFound(false);
    try {
      const token = await getToken();
      setApplication(await getApplication(token, params.id));
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        setNotFound(true);
      } else {
        setLoadError(formError(error));
      }
    } finally {
      setLoading(false);
    }
  }, [params.id, getToken]);

  React.useEffect(() => {
    // Fetch-on-mount: same documented pattern as every other Phase 2.1/2.2/2.3 page.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  async function onDownload() {
    if (!application) return;

    setDownloading(true);
    setDownloadError(undefined);
    try {
      const token = await getToken();
      await downloadResume(token, application.id, application.resumeFileName);
    } catch (error) {
      setDownloadError(formError(error));
    } finally {
      setDownloading(false);
    }
  }

  return (
    <DashboardShell nav={NAV} navLabel="Applicant dashboard">
      <div className="max-w-3xl space-y-6">
        <Link
          href="/dashboard/applications"
          className="text-small font-medium text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          &larr; Back to my applications
        </Link>

        {searchParams.get("submitted") === "true" ? (
          <Alert tone="success" title="Application submitted">
            Your application has been submitted successfully.
          </Alert>
        ) : null}

        {loading ? (
          <div className="space-y-4">
            <Skeleton className="h-10 w-2/3" />
            <Skeleton className="h-48 w-full" />
          </div>
        ) : notFound ? (
          <Alert tone="error" title="Application not found">
            This application doesn&rsquo;t exist or isn&rsquo;t yours.
          </Alert>
        ) : loadError ? (
          <Alert tone="error" title="Couldn't load this application">
            {loadError}
            <div className="mt-3">
              <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
                Try again
              </Button>
            </div>
          </Alert>
        ) : application ? (
          <Card className="space-y-4 p-6">
            <div className="flex flex-wrap items-start justify-between gap-4 border-b border-border pb-4">
              <div className="flex items-start gap-3">
                <span
                  className="flex size-12 shrink-0 items-center justify-center rounded-md bg-primary-subtle text-primary"
                  aria-hidden="true"
                >
                  <Building2 className="size-6" />
                </span>
                <div className="space-y-1">
                  <h1 className="text-h2 text-foreground">{application.jobTitle}</h1>
                  <p className="text-body font-medium text-foreground-secondary">{application.companyName}</p>
                </div>
              </div>
              <Badge tone="info" variant="subtle" showDot>
                {application.status}
              </Badge>
            </div>

            <p className="text-caption text-muted-foreground">Submitted {formatDate(application.createdAt)}</p>

            {application.coverLetter ? (
              <div className="space-y-2">
                <h2 className="text-h4 text-foreground">Cover letter</h2>
                <p className="whitespace-pre-line text-body text-foreground-secondary">{application.coverLetter}</p>
              </div>
            ) : null}

            <div className="space-y-2 border-t border-border pt-4">
              {downloadError ? (
                <Alert tone="error" title="Couldn't download your resume">
                  {downloadError}
                </Alert>
              ) : null}
              <Button variant="outline" loading={downloading} onClick={() => void onDownload()}>
                <Download />
                Download resume ({application.resumeFileName})
              </Button>
            </div>
          </Card>
        ) : null}
      </div>
    </DashboardShell>
  );
}

export default function ApplicationDetailsPage() {
  return (
    <React.Suspense fallback={null}>
      <ApplicationDetailsView />
    </React.Suspense>
  );
}
