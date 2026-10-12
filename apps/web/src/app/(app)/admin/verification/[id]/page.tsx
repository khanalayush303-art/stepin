"use client";

import * as React from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { formError } from "@/lib/auth/client";
import { approveExperienceRecord, beginExperienceReview, getExperienceRecordAdmin, rejectExperienceRecord } from "@/lib/experience/api";
import type { ExperienceAdminRecord, ExperienceVerificationStatus } from "@/lib/experience/types";
import { ADMIN_NAV } from "../../_nav";

const STATUS_TONE: Record<ExperienceVerificationStatus, "neutral" | "info" | "success" | "error" | "warning"> = {
  Draft: "neutral",
  Submitted: "info",
  UnderReview: "warning",
  Verified: "success",
  Rejected: "error",
  Withdrawn: "neutral",
};

export default function AdminExperienceRecordPage() {
  const { id } = useParams<{ id: string }>();
  const { getToken } = useAuth();

  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [record, setRecord] = React.useState<ExperienceAdminRecord | null>(null);
  const [note, setNote] = React.useState("");
  const [pending, setPending] = React.useState(false);
  const [actionError, setActionError] = React.useState<string>();

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      setRecord(await getExperienceRecordAdmin(token, id));
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

  async function onBeginReview() {
    setActionError(undefined);
    setPending(true);
    try {
      const token = await getToken();
      setRecord(await beginExperienceReview(token, id));
    } catch (error) {
      setActionError(formError(error));
    } finally {
      setPending(false);
    }
  }

  async function onApprove() {
    if (!window.confirm("Approve this record? This marks it Verified for the candidate.")) return;
    setActionError(undefined);
    setPending(true);
    try {
      const token = await getToken();
      setRecord(await approveExperienceRecord(token, id, note.trim() || null));
      setNote("");
    } catch (error) {
      setActionError(formError(error));
    } finally {
      setPending(false);
    }
  }

  async function onReject() {
    if (!window.confirm("Reject this record? The candidate will see your note and can correct and resubmit it.")) return;
    setActionError(undefined);
    setPending(true);
    try {
      const token = await getToken();
      setRecord(await rejectExperienceRecord(token, id, note.trim() || null));
      setNote("");
    } catch (error) {
      setActionError(formError(error));
    } finally {
      setPending(false);
    }
  }

  if (loading) {
    return (
      <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
        <div className="space-y-4">
          <Skeleton className="h-10 w-1/2" />
          <Skeleton className="h-96 w-full" />
        </div>
      </DashboardShell>
    );
  }

  if (loadError || !record) {
    return (
      <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
        <Alert tone="error" title="Couldn't load this record">
          {loadError ?? "It may not exist."}
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
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="space-y-6">
        <Link href="/admin/verification" className="inline-flex items-center gap-1.5 text-small font-medium text-foreground-secondary hover:text-foreground">
          <ArrowLeft className="size-4" />
          Verification queue
        </Link>

        <Card className="space-y-4 p-6">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div className="space-y-1">
              <p className="text-caption font-medium uppercase tracking-wide text-muted-foreground">{record.recordType}</p>
              <h1 className="text-h2 text-foreground">{record.title}</h1>
              <p className="text-small text-foreground-secondary">
                {record.applicantName} · {record.applicantEmail}
                {record.organization ? ` · ${record.organization}` : ""}
              </p>
            </div>
            <Badge tone={STATUS_TONE[record.status]} variant="subtle">
              {record.status}
            </Badge>
          </div>

          {record.description ? <p className="text-body text-foreground-secondary">{record.description}</p> : null}

          {record.skills.length > 0 ? (
            <div className="flex flex-wrap gap-1.5">
              {record.skills.map((skill) => (
                <Badge key={skill} tone="neutral" variant="subtle">
                  {skill}
                </Badge>
              ))}
            </div>
          ) : null}

          {record.evidenceUrl ? (
            <p className="text-small">
              <span className="font-medium text-foreground">Supporting link: </span>
              <a href={record.evidenceUrl} target="_blank" rel="noreferrer" className="text-primary underline">
                {record.evidenceUrl}
              </a>
              <span className="ml-2 text-caption text-muted-foreground">(candidate-provided, not independently verified)</span>
            </p>
          ) : null}

          {actionError ? <p className="text-small text-error">{actionError}</p> : null}

          <div className="space-y-3 border-t border-border pt-4">
            {record.status === "Submitted" ? (
              <Button size="sm" onClick={() => void onBeginReview()} disabled={pending}>
                Begin review
              </Button>
            ) : null}

            {record.status === "UnderReview" ? (
              <>
                <Textarea value={note} onChange={(e) => setNote(e.target.value)} placeholder="Optional note for the candidate…" rows={3} />
                <div className="flex gap-2">
                  <Button size="sm" onClick={() => void onApprove()} disabled={pending}>
                    Approve
                  </Button>
                  <Button size="sm" variant="outline" className="text-error" onClick={() => void onReject()} disabled={pending}>
                    Reject
                  </Button>
                </div>
              </>
            ) : null}

            {record.status === "Verified" || record.status === "Rejected" ? (
              <p className="text-small text-muted-foreground">This record has already been decided. See the audit history below.</p>
            ) : null}
          </div>
        </Card>

        <Card className="space-y-3 p-6">
          <h2 className="text-h4 text-foreground">Audit history</h2>
          {record.auditHistory.length === 0 ? (
            <p className="text-small text-muted-foreground">No verification events yet.</p>
          ) : (
            <ul className="space-y-2">
              {record.auditHistory.map((entry, index) => (
                <li key={index} className="flex flex-wrap items-baseline gap-2 border-b border-border pb-2 text-small last:border-0 last:pb-0">
                  <span className="font-medium text-foreground">{entry.action}</span>
                  <span className="text-muted-foreground">
                    {entry.previousStatus} → {entry.newStatus}
                  </span>
                  <span className="text-caption text-muted-foreground">
                    by {entry.actorName} ({entry.actorRole}) · {new Date(entry.createdAt).toLocaleString()}
                  </span>
                  {entry.note ? <p className="w-full text-foreground-secondary">{entry.note}</p> : null}
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>
    </DashboardShell>
  );
}
