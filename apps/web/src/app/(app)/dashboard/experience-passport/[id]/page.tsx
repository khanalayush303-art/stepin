"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { ArrowLeft, Trash2 } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { CANDIDATE_NAV } from "@/components/dashboard/candidate-nav";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { ApiError, formError } from "@/lib/auth/client";
import {
  deleteExperienceRecord,
  getExperienceRecord,
  resubmitExperienceRecord,
  submitExperienceRecord,
  updateExperienceRecord,
  withdrawExperienceRecord,
} from "@/lib/experience/api";
import type { ExperienceRecord, ExperienceRecordType, ExperienceVisibility } from "@/lib/experience/types";

const NAV = CANDIDATE_NAV;
const RECORD_TYPES: ExperienceRecordType[] = ["Employment", "Internship", "Project", "Research", "Volunteering", "Certification"];

const STATUS_TONE: Record<ExperienceRecord["status"], "neutral" | "info" | "success" | "error" | "warning"> = {
  Draft: "neutral",
  Submitted: "info",
  UnderReview: "warning",
  Verified: "success",
  Rejected: "error",
  Withdrawn: "neutral",
};

const STATUS_EXPLANATION: Record<ExperienceRecord["status"], string> = {
  Draft: "Not yet submitted. Only you can see this.",
  Submitted: "Waiting for a StepIn administrator to begin review.",
  UnderReview: "A StepIn administrator is currently reviewing this record.",
  Verified: "A StepIn administrator reviewed and approved this record.",
  Rejected: "A StepIn administrator reviewed this record and did not approve it. See the reason below — you can correct and resubmit it.",
  Withdrawn: "You withdrew this record from review. You can edit and resubmit it.",
};

export default function ExperienceRecordDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { getToken } = useAuth();
  const router = useRouter();

  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [record, setRecord] = React.useState<ExperienceRecord | null>(null);

  const [recordType, setRecordType] = React.useState<ExperienceRecordType>("Employment");
  const [title, setTitle] = React.useState("");
  const [organization, setOrganization] = React.useState("");
  const [description, setDescription] = React.useState("");
  const [startDate, setStartDate] = React.useState("");
  const [endDate, setEndDate] = React.useState("");
  const [skills, setSkills] = React.useState("");
  const [evidenceUrl, setEvidenceUrl] = React.useState("");
  const [visibility, setVisibility] = React.useState<ExperienceVisibility>("Private");

  const [fieldErrors, setFieldErrors] = React.useState<Record<string, string[]>>({});
  const [saving, setSaving] = React.useState(false);
  const [saveError, setSaveError] = React.useState<string>();
  const [saved, setSaved] = React.useState(false);
  const [actionError, setActionError] = React.useState<string>();
  const [actionPending, setActionPending] = React.useState(false);

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      const loaded = await getExperienceRecord(token, id);
      setRecord(loaded);
      setRecordType(loaded.recordType);
      setTitle(loaded.title);
      setOrganization(loaded.organization ?? "");
      setDescription(loaded.description ?? "");
      setStartDate(loaded.startDate ?? "");
      setEndDate(loaded.endDate ?? "");
      setSkills(loaded.skills.join(", "));
      setEvidenceUrl(loaded.evidenceUrl ?? "");
      setVisibility(loaded.visibility);
    } catch (error) {
      setLoadError(formError(error));
    } finally {
      setLoading(false);
    }
  }, [getToken, id]);

  React.useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  async function onSave(event: React.FormEvent) {
    event.preventDefault();
    setFieldErrors({});
    setSaveError(undefined);
    setSaved(false);
    setSaving(true);
    try {
      const token = await getToken();
      const updated = await updateExperienceRecord(token, id, {
        recordType,
        title: title.trim(),
        organization: organization.trim() || null,
        description: description.trim() || null,
        startDate: startDate || null,
        endDate: endDate || null,
        skills: skills.split(",").map((s) => s.trim()).filter(Boolean),
        evidenceUrl: evidenceUrl.trim() || null,
        visibility,
      });
      setRecord(updated);
      setSaved(true);
    } catch (error) {
      if (error instanceof ApiError && error.problem.errors) {
        setFieldErrors(error.problem.errors);
      }
      setSaveError(formError(error));
    } finally {
      setSaving(false);
    }
  }

  async function runAction(action: () => Promise<ExperienceRecord>) {
    setActionError(undefined);
    setActionPending(true);
    try {
      setRecord(await action());
    } catch (error) {
      setActionError(formError(error));
    } finally {
      setActionPending(false);
    }
  }

  async function onSubmitForReview() {
    if (!window.confirm("Submit this record for review? You won't be able to edit it while it's being reviewed.")) return;
    const token = await getToken();
    await runAction(() => submitExperienceRecord(token, id));
  }

  async function onResubmit() {
    if (!window.confirm("Resubmit this record for review?")) return;
    const token = await getToken();
    await runAction(() => resubmitExperienceRecord(token, id));
  }

  async function onWithdraw() {
    if (!window.confirm("Withdraw this record from review? You'll be able to edit and resubmit it afterward.")) return;
    const token = await getToken();
    await runAction(() => withdrawExperienceRecord(token, id, null));
  }

  async function onDelete() {
    if (!record || !window.confirm("Delete this draft record? This can't be undone.")) return;
    try {
      const token = await getToken();
      await deleteExperienceRecord(token, record.id);
      router.push("/dashboard/experience-passport");
    } catch (error) {
      setActionError(formError(error));
    }
  }

  if (loading) {
    return (
      <DashboardShell nav={NAV} navLabel="Applicant dashboard">
        <div className="space-y-4">
          <Skeleton className="h-10 w-1/2" />
          <Skeleton className="h-96 w-full" />
        </div>
      </DashboardShell>
    );
  }

  if (loadError || !record) {
    return (
      <DashboardShell nav={NAV} navLabel="Applicant dashboard">
        <Alert tone="error" title="Couldn't load this record">
          {loadError ?? "It may not exist, or it may not be yours."}
          <div className="mt-3">
            <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
              Try again
            </Button>
          </div>
        </Alert>
      </DashboardShell>
    );
  }

  return (
    <DashboardShell nav={NAV} navLabel="Applicant dashboard">
      <div className="space-y-6">
        <Link
          href="/dashboard/experience-passport"
          className="inline-flex items-center gap-1.5 text-small font-medium text-foreground-secondary hover:text-foreground"
        >
          <ArrowLeft className="size-4" />
          All experience records
        </Link>

        <Card className="space-y-2 p-6">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h1 className="text-h2 text-foreground">{record.title}</h1>
            <Badge tone={STATUS_TONE[record.status]} variant="subtle">
              {record.status}
            </Badge>
          </div>
          <p className="text-small text-muted-foreground">{STATUS_EXPLANATION[record.status]}</p>

          {actionError ? <p className="text-small text-error">{actionError}</p> : null}

          <div className="flex flex-wrap gap-2 pt-2">
            {record.status === "Draft" ? (
              <>
                <Button size="sm" onClick={() => void onSubmitForReview()} disabled={actionPending}>
                  Submit for review
                </Button>
                <Button size="sm" variant="ghost" className="text-error" onClick={() => void onDelete()}>
                  <Trash2 className="size-4" />
                  Delete
                </Button>
              </>
            ) : null}
            {(record.status === "Rejected" || record.status === "Withdrawn") ? (
              <Button size="sm" onClick={() => void onResubmit()} disabled={actionPending}>
                Resubmit for review
              </Button>
            ) : null}
            {(record.status === "Submitted" || record.status === "UnderReview" || record.status === "Verified") ? (
              <Button size="sm" variant="outline" onClick={() => void onWithdraw()} disabled={actionPending}>
                Withdraw
              </Button>
            ) : null}
          </div>
        </Card>

        {record.timeline.length > 0 ? (
          <Card className="space-y-3 p-6">
            <h2 className="text-h4 text-foreground">Status timeline</h2>
            <ul className="space-y-2">
              {record.timeline.map((entry, index) => (
                <li key={index} className="flex flex-wrap items-baseline gap-2 border-b border-border pb-2 text-small last:border-0 last:pb-0">
                  <span className="font-medium text-foreground">{entry.action}</span>
                  <span className="text-muted-foreground">
                    {entry.previousStatus} → {entry.newStatus}
                  </span>
                  <span className="text-caption text-muted-foreground">{new Date(entry.createdAt).toLocaleString()}</span>
                  {entry.note ? <p className="w-full text-foreground-secondary">{entry.note}</p> : null}
                </li>
              ))}
            </ul>
          </Card>
        ) : null}

        <Card className="space-y-4 p-6">
          <form onSubmit={onSave} className="space-y-4">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <h2 className="text-h3 text-foreground">{record.canEdit ? "Edit record" : "Record details"}</h2>
              {record.canEdit ? (
                <div className="flex items-center gap-3 rounded-lg border border-border bg-surface-subtle px-3 py-2">
                  <div>
                    <p className="text-small font-medium text-foreground">{visibility === "Public" ? "Public" : "Private"}</p>
                    <p className="text-caption text-muted-foreground">
                      {visibility === "Public" ? "Visible to recruiters you've applied to." : "Only visible to you."}
                    </p>
                  </div>
                  <Switch checked={visibility === "Public"} onCheckedChange={(c) => setVisibility(c ? "Public" : "Private")} aria-label="Toggle visibility" />
                </div>
              ) : (
                <Badge tone={visibility === "Public" ? "success" : "neutral"} variant="subtle">
                  {visibility}
                </Badge>
              )}
            </div>

            <div className="grid gap-3 sm:grid-cols-3">
              <div>
                <span className="mb-1.5 block text-small font-medium text-foreground">Type</span>
                <Select value={recordType} onValueChange={(v) => setRecordType(v as ExperienceRecordType)} disabled={!record.canEdit}>
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
                {(props) => <Input {...props} value={title} onChange={(e) => setTitle(e.target.value)} readOnly={!record.canEdit} required />}
              </FormField>
              <FormField id="organization" label="Organization" error={fieldErrors.organization?.[0]}>
                {(props) => <Input {...props} value={organization} onChange={(e) => setOrganization(e.target.value)} readOnly={!record.canEdit} />}
              </FormField>
            </div>

            <FormField id="description" label="Description" error={fieldErrors.description?.[0]}>
              {(props) => <Textarea {...props} value={description} onChange={(e) => setDescription(e.target.value)} rows={4} readOnly={!record.canEdit} />}
            </FormField>

            <div className="grid gap-3 sm:grid-cols-2">
              <FormField id="startDate" label="Start date">
                {(props) => <Input {...props} type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} readOnly={!record.canEdit} />}
              </FormField>
              <FormField id="endDate" label="End date" error={fieldErrors.endDate?.[0]}>
                {(props) => <Input {...props} type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} readOnly={!record.canEdit} />}
              </FormField>
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <FormField id="skills" label="Skills (comma-separated)" error={fieldErrors.skills?.[0]}>
                {(props) => <Input {...props} value={skills} onChange={(e) => setSkills(e.target.value)} readOnly={!record.canEdit} />}
              </FormField>
              <FormField id="evidenceUrl" label="Evidence URL (optional)" error={fieldErrors.evidenceUrl?.[0]}>
                {(props) => (
                  <Input {...props} value={evidenceUrl} onChange={(e) => setEvidenceUrl(e.target.value)} placeholder="https://…" readOnly={!record.canEdit} />
                )}
              </FormField>
            </div>
            <p className="text-caption text-muted-foreground">
              A supporting link is not independent proof — it&apos;s shown as reference only and is never itself treated as verification.
            </p>

            {record.canEdit ? (
              <div className="flex items-center justify-between border-t border-border pt-4">
                {saveError ? <p className="text-small text-error">{saveError}</p> : <span />}
                {saved ? <p className="text-small text-success">Saved.</p> : null}
                <Button type="submit" disabled={saving || !title.trim()}>
                  {saving ? "Saving…" : "Save changes"}
                </Button>
              </div>
            ) : null}
          </form>
        </Card>
      </div>
    </DashboardShell>
  );
}
