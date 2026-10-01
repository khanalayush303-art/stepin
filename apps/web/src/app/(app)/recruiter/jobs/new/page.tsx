"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { Building2, Clock, FileText, LayoutDashboard, Users } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { DashboardShell, type DashboardNavItem } from "@/components/dashboard/dashboard-shell";
import { ApiError, formError } from "@/lib/auth/client";
import { createJob } from "@/lib/jobs/api";
import type { JobInput } from "@/lib/jobs/types";
import { JobForm } from "../_components/job-form";

const NAV: DashboardNavItem[] = [
  { href: "/recruiter", label: "Overview", icon: LayoutDashboard },
  { href: "/recruiter/jobs", label: "Job listings", icon: FileText },
  { href: "/recruiter/applications", label: "Applications", icon: Users },
  { href: "/recruiter/interviews", label: "Interviews", icon: Clock, badge: "5" },
  { href: "/recruiter/profile", label: "Company profile", icon: Building2 },
];

const EMPTY: JobInput = {
  title: "",
  description: "",
  employmentType: "",
  workplaceType: "",
  location: "",
  compensation: null,
  skills: [],
};

export default function NewJobPage() {
  const router = useRouter();
  const { getToken } = useAuth();
  const [value, setValue] = React.useState<JobInput>(EMPTY);
  const [saving, setSaving] = React.useState(false);
  const [serverError, setServerError] = React.useState<string>();
  const [fieldErrors, setFieldErrors] = React.useState<Record<string, string[]>>({});

  async function onSave() {
    setSaving(true);
    setServerError(undefined);
    setFieldErrors({});
    try {
      const token = await getToken();
      const job = await createJob(token, value);
      router.push(`/recruiter/jobs/${job.id}/edit`);
    } catch (error) {
      if (error instanceof ApiError && error.problem.errors) {
        setFieldErrors(error.problem.errors);
      }
      setServerError(formError(error));
      setSaving(false);
    }
  }

  return (
    <DashboardShell nav={NAV} navLabel="Recruiter dashboard">
      <div className="max-w-3xl space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Post a role</h1>
          <p className="text-body text-muted-foreground">
            Saved as a draft first — publish it when you&rsquo;re ready for candidates to see it.
          </p>
        </div>

        {serverError ? (
          <Alert tone="error" title="Couldn't create this job">
            {serverError}
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
          <div className="flex justify-end">
            <Button type="submit" size="lg" loading={saving} loadingText="Saving…">
              Save draft
            </Button>
          </div>
        </form>
      </div>
    </DashboardShell>
  );
}
