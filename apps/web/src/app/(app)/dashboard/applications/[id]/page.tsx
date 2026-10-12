"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useSearchParams } from "next/navigation";
import { Building2, Download } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { ApplicationStatusBadge } from "@/components/applications/application-status-badge";
import { CANDIDATE_NAV } from "@/components/dashboard/candidate-nav";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { ApiError, formError } from "@/lib/auth/client";
import { downloadResume, getApplication } from "@/lib/applications/api";
import type { Application } from "@/lib/applications/types";
import { formatDate } from "@/lib/utils";

const NAV = CANDIDATE_NAV;

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
              <ApplicationStatusBadge status={application.status} />
            </div>

            <div className="flex flex-wrap gap-x-4 gap-y-1 text-caption text-muted-foreground">
              <span>Submitted {formatDate(application.createdAt)}</span>
              <span>Status last updated {formatDate(application.statusUpdatedAt)}</span>
            </div>

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
