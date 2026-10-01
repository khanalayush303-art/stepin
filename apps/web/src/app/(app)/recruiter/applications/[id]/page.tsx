"use client";

import * as React from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { Building2, Clock, Download, FileText, LayoutDashboard, Mail, User, Users } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { DashboardShell, type DashboardNavItem } from "@/components/dashboard/dashboard-shell";
import { ApiError, formError } from "@/lib/auth/client";
import { downloadRecruiterResume, getRecruiterApplication, updateRecruiterApplicationStatus } from "@/lib/applications/api";
import { APPLICATION_STATUSES, type ApplicationStatus, type RecruiterApplication } from "@/lib/applications/types";
import { formatDate } from "@/lib/utils";
import { ApplicationStatusBadge } from "../_components/application-status-badge";

const NAV: DashboardNavItem[] = [
  { href: "/recruiter", label: "Overview", icon: LayoutDashboard },
  { href: "/recruiter/jobs", label: "Job listings", icon: FileText },
  { href: "/recruiter/applications", label: "Applications", icon: Users },
  { href: "/recruiter/interviews", label: "Interviews", icon: Clock, badge: "5" },
  { href: "/recruiter/profile", label: "Company profile", icon: Building2 },
];

export default function RecruiterApplicationDetailPage() {
  const params = useParams<{ id: string }>();
  const { getToken } = useAuth();

  const [application, setApplication] = React.useState<RecruiterApplication | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [notFound, setNotFound] = React.useState(false);
  const [downloading, setDownloading] = React.useState(false);
  const [downloadError, setDownloadError] = React.useState<string>();
  const [statusDraft, setStatusDraft] = React.useState<ApplicationStatus>("Submitted");
  const [updatingStatus, setUpdatingStatus] = React.useState(false);
  const [statusError, setStatusError] = React.useState<string>();

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    setNotFound(false);
    try {
      const token = await getToken();
      const loaded = await getRecruiterApplication(token, params.id);
      setApplication(loaded);
      setStatusDraft(loaded.status);
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
    // Fetch-on-mount: same documented pattern as every other dashboard page.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  async function onDownload() {
    if (!application) return;

    setDownloading(true);
    setDownloadError(undefined);
    try {
      const token = await getToken();
      await downloadRecruiterResume(token, application.id, application.resumeFileName);
    } catch (error) {
      setDownloadError(formError(error));
    } finally {
      setDownloading(false);
    }
  }

  async function onUpdateStatus() {
    if (!application || statusDraft === application.status) return;

    setUpdatingStatus(true);
    setStatusError(undefined);
    try {
      const token = await getToken();
      const updated = await updateRecruiterApplicationStatus(token, application.id, statusDraft);
      // Only reflect the change once the server confirms it — never update
      // the displayed status optimistically.
      setApplication(updated);
      setStatusDraft(updated.status);
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        setStatusError("This application is no longer available.");
      } else {
        setStatusError(formError(error));
      }
      // Revert the draft back to the last known-good status on failure, so
      // the picker never silently shows a status the server rejected.
      setStatusDraft(application.status);
    } finally {
      setUpdatingStatus(false);
    }
  }

  return (
    <DashboardShell nav={NAV} navLabel="Recruiter dashboard">
      <div className="max-w-3xl space-y-6">
        <Link
          href={application ? `/recruiter/applications?jobId=${application.jobId}` : "/recruiter/applications"}
          className="text-small font-medium text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          &larr; Back to applications
        </Link>

        {loading ? (
          <div className="space-y-4">
            <Skeleton className="h-10 w-2/3" />
            <Skeleton className="h-48 w-full" />
          </div>
        ) : notFound ? (
          <Alert tone="error" title="Application not found">
            This application doesn&rsquo;t exist, or it isn&rsquo;t on one of your jobs.
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
                  <User className="size-6" />
                </span>
                <div className="space-y-1">
                  <h1 className="text-h2 text-foreground">{application.applicantName}</h1>
                  <p className="flex items-center gap-1.5 text-small text-foreground-secondary">
                    <Mail className="size-4 shrink-0" aria-hidden="true" />
                    {application.applicantEmail}
                  </p>
                </div>
              </div>
              <ApplicationStatusBadge status={application.status} />
            </div>

            <p className="text-small text-foreground-secondary">
              Applied for <span className="font-medium text-foreground">{application.jobTitle}</span>
            </p>
            <p className="text-caption text-muted-foreground">Submitted {formatDate(application.createdAt)}</p>

            <div className="space-y-2 border-t border-border pt-4">
              <p className="text-small font-medium text-foreground">Status</p>
              {statusError ? (
                <Alert tone="error" title="Couldn't update status">
                  {statusError}
                </Alert>
              ) : null}
              <div className="flex flex-wrap items-center gap-3">
                <Select
                  value={statusDraft}
                  onValueChange={(value) => setStatusDraft(value as ApplicationStatus)}
                  disabled={updatingStatus}
                >
                  <SelectTrigger className="w-[180px]">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {APPLICATION_STATUSES.map((value) => (
                      <SelectItem key={value} value={value}>
                        {value}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Button
                  size="sm"
                  loading={updatingStatus}
                  disabled={statusDraft === application.status}
                  onClick={() => void onUpdateStatus()}
                >
                  Update status
                </Button>
              </div>
            </div>

            {application.coverLetter ? (
              <div className="space-y-2">
                <h2 className="text-h4 text-foreground">Cover letter</h2>
                <p className="whitespace-pre-line text-body text-foreground-secondary">{application.coverLetter}</p>
              </div>
            ) : null}

            <div className="space-y-2 border-t border-border pt-4">
              {downloadError ? (
                <Alert tone="error" title="Couldn't download this resume">
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
