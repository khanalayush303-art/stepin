"use client";

import * as React from "react";
import Link from "next/link";
import { BookUser, Lock } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { CANDIDATE_NAV } from "@/components/dashboard/candidate-nav";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { ApiError, formError } from "@/lib/auth/client";
import { createExperienceRecord, listExperienceRecords } from "@/lib/experience/api";
import type { ExperienceRecordSummary, ExperienceRecordType } from "@/lib/experience/types";

const NAV = CANDIDATE_NAV;

const RECORD_TYPES: ExperienceRecordType[] = ["Employment", "Internship", "Project", "Research", "Volunteering", "Certification"];

const STATUS_TONE: Record<ExperienceRecordSummary["status"], "neutral" | "info" | "success" | "error" | "warning"> = {
  Draft: "neutral",
  Submitted: "info",
  UnderReview: "warning",
  Verified: "success",
  Rejected: "error",
  Withdrawn: "neutral",
};

export default function ExperiencePassportPage() {
  const { getToken } = useAuth();
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [records, setRecords] = React.useState<ExperienceRecordSummary[]>([]);

  const [recordType, setRecordType] = React.useState<ExperienceRecordType>("Employment");
  const [title, setTitle] = React.useState("");
  const [organization, setOrganization] = React.useState("");
  const [description, setDescription] = React.useState("");
  const [skills, setSkills] = React.useState("");
  const [fieldErrors, setFieldErrors] = React.useState<Record<string, string[]>>({});
  const [submitting, setSubmitting] = React.useState(false);
  const [submitError, setSubmitError] = React.useState<string>();

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      setRecords(await listExperienceRecords(token));
    } catch (error) {
      setLoadError(formError(error));
    } finally {
      setLoading(false);
    }
  }, [getToken]);

  React.useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  async function onCreate(event: React.FormEvent) {
    event.preventDefault();
    setFieldErrors({});
    setSubmitError(undefined);
    setSubmitting(true);
    try {
      const token = await getToken();
      const record = await createExperienceRecord(token, {
        recordType,
        title: title.trim(),
        organization: organization.trim() || null,
        description: description.trim() || null,
        startDate: null,
        endDate: null,
        skills: skills.split(",").map((s) => s.trim()).filter(Boolean),
        evidenceUrl: null,
        visibility: "Private",
      });
      setRecords((current) => [
        {
          id: record.id,
          recordType: record.recordType,
          title: record.title,
          organization: record.organization,
          visibility: record.visibility,
          status: record.status,
          startDate: record.startDate,
          endDate: record.endDate,
          createdAt: record.createdAt,
          updatedAt: record.updatedAt,
        },
        ...current,
      ]);
      setTitle("");
      setOrganization("");
      setDescription("");
      setSkills("");
    } catch (error) {
      if (error instanceof ApiError && error.problem.errors) {
        setFieldErrors(error.problem.errors);
      }
      setSubmitError(formError(error));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <DashboardShell nav={NAV} navLabel="Applicant dashboard">
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Experience Passport</h1>
          <p className="text-body text-muted-foreground">
            Record your experience and optionally submit it for review by a StepIn administrator. New records start
            Private and Draft — nothing is shared or reviewed until you choose to submit it.
          </p>
        </div>

        <Card className="p-4">
          <form onSubmit={onCreate} className="space-y-3">
            <div className="grid gap-3 sm:grid-cols-3">
              <div>
                <span className="mb-1.5 block text-small font-medium text-foreground">Type</span>
                <Select value={recordType} onValueChange={(v) => setRecordType(v as ExperienceRecordType)}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {RECORD_TYPES.map((type) => (
                      <SelectItem key={type} value={type}>
                        {type}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <FormField id="title" label="Title / role" error={fieldErrors.title?.[0]}>
                {(props) => <Input {...props} value={title} onChange={(e) => setTitle(e.target.value)} placeholder="e.g. Software Engineering Intern" required />}
              </FormField>
              <FormField id="organization" label="Organization (optional)" error={fieldErrors.organization?.[0]}>
                {(props) => <Input {...props} value={organization} onChange={(e) => setOrganization(e.target.value)} placeholder="e.g. Acme Corp" />}
              </FormField>
            </div>

            <FormField id="description" label="Description (optional)" error={fieldErrors.description?.[0]}>
              {(props) => <Textarea {...props} value={description} onChange={(e) => setDescription(e.target.value)} rows={3} />}
            </FormField>

            <FormField id="skills" label="Skills (comma-separated, optional)" error={fieldErrors.skills?.[0]}>
              {(props) => <Input {...props} value={skills} onChange={(e) => setSkills(e.target.value)} placeholder="e.g. C#, React" />}
            </FormField>

            <div className="flex items-center justify-between">
              {submitError ? <p className="text-small text-error">{submitError}</p> : <span />}
              <Button type="submit" disabled={submitting || !title.trim()}>
                {submitting ? "Adding…" : "Add record"}
              </Button>
            </div>
          </form>
        </Card>

        {loading ? (
          <div className="space-y-4">
            <Skeleton className="h-28 w-full" />
            <Skeleton className="h-28 w-full" />
          </div>
        ) : loadError ? (
          <Alert tone="error" title="Couldn't load your experience records">
            {loadError}
            <div className="mt-3">
              <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
                Try again
              </Button>
            </div>
          </Alert>
        ) : records.length === 0 ? (
          <EmptyState
            icon={<BookUser className="size-6" />}
            title="No experience records yet"
            description="Add one above to start building your Experience Passport."
          />
        ) : (
          <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {records.map((record) => (
              <li key={record.id}>
                <Card interactive className="flex h-full flex-col gap-3 p-6">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0 flex-1 space-y-1">
                      <p className="text-caption font-medium uppercase tracking-wide text-muted-foreground">{record.recordType}</p>
                      <h2 className="text-h4 text-foreground">
                        <Link
                          href={`/dashboard/experience-passport/${record.id}`}
                          className="rounded-sm after:absolute after:inset-0 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                        >
                          {record.title}
                        </Link>
                      </h2>
                      {record.organization ? <p className="truncate text-small text-foreground-secondary">{record.organization}</p> : null}
                    </div>
                    <div className="flex shrink-0 flex-col items-end gap-1">
                      <Badge tone={STATUS_TONE[record.status]} variant="subtle">
                        {record.status}
                      </Badge>
                      {record.visibility === "Private" ? (
                        <Badge tone="neutral" variant="subtle">
                          <span className="inline-flex items-center gap-1">
                            <Lock className="size-3" /> Private
                          </span>
                        </Badge>
                      ) : null}
                    </div>
                  </div>

                  <Button size="sm" variant="outline" asChild className="relative z-10 mt-auto w-fit">
                    <Link href={`/dashboard/experience-passport/${record.id}`} tabIndex={-1}>
                      Open
                    </Link>
                  </Button>
                </Card>
              </li>
            ))}
          </ul>
        )}
      </div>
    </DashboardShell>
  );
}
