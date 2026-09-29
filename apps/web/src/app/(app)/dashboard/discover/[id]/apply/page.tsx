"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { Bell, Bookmark, FileText, GraduationCap, LayoutDashboard, Search, User } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { DashboardShell, type DashboardNavItem } from "@/components/dashboard/dashboard-shell";
import { ApiError, formError } from "@/lib/auth/client";
import { submitApplication } from "@/lib/applications/api";

const NAV: DashboardNavItem[] = [
  { href: "/dashboard", label: "Overview", icon: LayoutDashboard },
  { href: "/dashboard/discover", label: "Discover roles", icon: Search },
  { href: "/dashboard/applications", label: "Applications", icon: FileText, badge: "12" },
  { href: "/dashboard/saved", label: "Saved roles", icon: Bookmark, badge: "8" },
  { href: "/dashboard/interviews", label: "Interviews", icon: User, badge: "2" },
  { href: "/dashboard/prepare", label: "Prepare", icon: GraduationCap },
  { href: "/dashboard/notifications", label: "Notifications", icon: Bell, badge: "3" },
];

export default function ApplyToJobPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const { getToken } = useAuth();

  const [resume, setResume] = React.useState<File | null>(null);
  const [coverLetter, setCoverLetter] = React.useState("");
  const [submitting, setSubmitting] = React.useState(false);
  const [serverError, setServerError] = React.useState<string>();
  const [fieldErrors, setFieldErrors] = React.useState<Record<string, string[]>>({});

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();

    if (!resume) {
      setFieldErrors({ resume: ["A resume file is required."] });
      return;
    }

    setSubmitting(true);
    setServerError(undefined);
    setFieldErrors({});

    try {
      const token = await getToken();
      const application = await submitApplication(token, params.id, resume, coverLetter);
      router.push(`/dashboard/applications/${application.id}?submitted=true`);
    } catch (error) {
      if (error instanceof ApiError && error.problem.errors) {
        setFieldErrors(error.problem.errors);
      }
      if (error instanceof ApiError && error.status === 409) {
        setServerError("You've already applied to this role.");
      } else {
        setServerError(formError(error));
      }
      setSubmitting(false);
    }
  }

  return (
    <DashboardShell nav={NAV} navLabel="Applicant dashboard">
      <div className="max-w-2xl space-y-6">
        <Link
          href={`/dashboard/discover/${params.id}`}
          className="text-small font-medium text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          &larr; Back to role
        </Link>

        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Apply for this role</h1>
          <p className="text-body text-muted-foreground">Attach your resume and, optionally, a cover letter.</p>
        </div>

        {serverError ? <Alert tone="error" title="Couldn't submit your application">{serverError}</Alert> : null}

        <Card className="p-6">
          <form onSubmit={onSubmit} className="space-y-5">
            <FormField id="resume" label="Resume" required error={fieldErrors.resume?.[0]} hint="PDF, DOC, or DOCX. Up to 5 MB.">
              {(props) => (
                <Input
                  {...props}
                  type="file"
                  accept=".pdf,.doc,.docx"
                  invalid={props.invalid}
                  onChange={(e) => setResume(e.target.files?.[0] ?? null)}
                />
              )}
            </FormField>

            <FormField id="coverLetter" label="Cover letter" error={fieldErrors.coverLetter?.[0]} hint="Optional.">
              {(props) => (
                <Textarea
                  {...props}
                  invalid={props.invalid}
                  rows={8}
                  value={coverLetter}
                  onChange={(e) => setCoverLetter(e.target.value)}
                  placeholder="Tell them why you're a great fit."
                />
              )}
            </FormField>

            <div className="flex items-center justify-end gap-3 border-t border-border pt-4">
              <Button type="button" variant="ghost" asChild>
                <Link href={`/dashboard/discover/${params.id}`}>Cancel</Link>
              </Button>
              <Button type="submit" loading={submitting}>
                Submit application
              </Button>
            </div>
          </form>
        </Card>
      </div>
    </DashboardShell>
  );
}
