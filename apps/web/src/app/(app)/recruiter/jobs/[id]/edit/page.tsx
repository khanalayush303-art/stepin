"use client";

import * as React from "react";
import { useParams, useRouter } from "next/navigation";
import { Building2, Clock, FileText, LayoutDashboard, Users } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { DashboardShell, type DashboardNavItem } from "@/components/dashboard/dashboard-shell";
import { ApiError, formError } from "@/lib/auth/client";
import { getJob, publishJob, unpublishJob, updateJob } from "@/lib/jobs/api";
import type { Job, JobInput } from "@/lib/jobs/types";
import { JobForm } from "../../_components/job-form";
import { JobStatusBadge } from "../../_components/job-status-badge";

const NAV: DashboardNavItem[] = [
  { href: "/recruiter", label: "Overview", icon: LayoutDashboard },
  { href: "/recruiter/jobs", label: "Job listings", icon: FileText },
  { href: "/recruiter/applications", label: "Applications", icon: Users },
  { href: "/recruiter/interviews", label: "Interviews", icon: Clock, badge: "5" },
  { href: "/recruiter/profile", label: "Company profile", icon: Building2 },
];

function toInput(job: Job): JobInput {
  return {
    title: job.title,
    description: job.description,
    employmentType: job.employmentType,
    workplaceType: job.workplaceType,
    location: job.location,
    compensation: job.compensation,
    skills: job.skills,
  };
}

export default function EditJobPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const { getToken } = useAuth();

  const [job, setJob] = React.useState<Job | null>(null);
  const [value, setValue] = React.useState<JobInput | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [saving, setSaving] = React.useState(false);
  const [publishing, setPublishing] = React.useState(false);
  const [serverError, setServerError] = React.useState<string>();
  const [fieldErrors, setFieldErrors] = React.useState<Record<string, string[]>>({});
  const [savedMessage, setSavedMessage] = React.useState(false);

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      const loaded = await getJob(token, params.id);
      setJob(loaded);
      setValue(toInput(loaded));
    } catch (error) {
      setLoadError(formError(error));
    } finally {
      setLoading(false);
    }
  }, [getToken, params.id]);

  React.useEffect(() => {
    // Fetch-on-mount: same documented pattern as the profile pages.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  async function onSave() {
    if (!value) return;

    setSaving(true);
    setServerError(undefined);
    setFieldErrors({});
    setSavedMessage(false);
    try {
      const token = await getToken();
      const updated = await updateJob(token, params.id, value);
      setJob(updated);
      setValue(toInput(updated));
      setSavedMessage(true);
    } catch (error) {
      if (error instanceof ApiError && error.problem.errors) {
        setFieldErrors(error.problem.errors);
      }
      setServerError(formError(error));
    } finally {
      setSaving(false);
    }
  }

  async function onTogglePublish() {
    if (!job) return;

    setPublishing(true);
    setServerError(undefined);
    try {
      const token = await getToken();
      const updated = job.status === "Published" ? await unpublishJob(token, job.id) : await publishJob(token, job.id);
      setJob(updated);
      setValue(toInput(updated));
    } catch (error) {
      setServerError(formError(error));
    } finally {
      setPublishing(false);
    }
  }

  return (
    <DashboardShell nav={NAV} navLabel="Recruiter dashboard">
      <div className="max-w-3xl space-y-6">
        {loading ? (
          <div className="space-y-4">
            <Skeleton className="h-10 w-1/2" />
            <Skeleton className="h-64 w-full" />
          </div>
        ) : loadError ? (
          <Alert tone="error" title="Couldn't load this job">
            {loadError}
            <div className="mt-3">
              <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
                Try again
              </Button>
            </div>
          </Alert>
        ) : job && value ? (
          <>
            <div className="flex flex-wrap items-center justify-between gap-4">
              <div className="space-y-1">
                <div className="flex flex-wrap items-center gap-3">
                  <h1 className="text-h2 text-foreground">Edit role</h1>
                  <JobStatusBadge status={job.status} />
                </div>
                <p className="text-body text-muted-foreground">{job.companyName}</p>
              </div>
              <Button
                type="button"
                variant="outline"
                loading={publishing}
                onClick={() => void onTogglePublish()}
              >
                {job.status === "Published" ? "Unpublish" : "Publish"}
              </Button>
            </div>

            {serverError ? (
              <Alert tone="error" title="Couldn't save this job">
                {serverError}
              </Alert>
            ) : null}

            {savedMessage ? (
              <Alert tone="success" title="Job saved">
                Your changes are live.
              </Alert>
            ) : null}

            <form
              onSubmit={(e) => {
                e.preventDefault();
                void onSave();
              }}
              className="space-y-6"
            >
              <JobForm value={value} onChange={setValue} fieldErrors={fieldErrors} disabled={saving} />
              <div className="flex justify-end gap-3">
                <Button type="button" variant="outline" onClick={() => router.push("/recruiter/jobs")}>
                  Back to my jobs
                </Button>
                <Button type="submit" size="lg" loading={saving} loadingText="Saving…">
                  Save changes
                </Button>
              </div>
            </form>
          </>
        ) : null}
      </div>
    </DashboardShell>
  );
}
