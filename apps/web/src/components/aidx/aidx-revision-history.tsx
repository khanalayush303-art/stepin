"use client";

import * as React from "react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { formatDateTime } from "@/lib/aidx/format";
import { formError } from "@/lib/auth/client";
import {
  listAuditEntries,
  listRevisions,
  type AidxAuditEntry,
  type AidxRevisionSummary,
  type AidxWorkflowContentType,
} from "@/lib/aidx/workflow";

/** Humanizes the backend's PascalCase AidxEditorialAction enum values for display. */
function actionLabel(action: string) {
  return action.replace(/([a-z])([A-Z])/g, "$1 $2");
}

function parseSnapshot(json: string): Record<string, unknown> {
  try {
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return {};
  }
}

function fieldValueText(value: unknown): string {
  if (value === null || value === undefined) return "—";
  if (typeof value === "object") return JSON.stringify(value);
  return String(value);
}

/** Field-by-field comparison of two submitted snapshots — not a text/token diff, just old vs new
 * per content field, which is enough to see what an edit actually changed. */
function SnapshotDiff({ before, after }: { before: AidxRevisionSummary; after: AidxRevisionSummary }) {
  const beforeFields = parseSnapshot(before.contentJson);
  const afterFields = parseSnapshot(after.contentJson);
  const keys = Array.from(new Set([...Object.keys(beforeFields), ...Object.keys(afterFields)])).filter(
    (key) => key !== "Id" && key !== "RevisionOfId" && key !== "RevisionNumber"
  );
  const changed = keys.filter((key) => fieldValueText(beforeFields[key]) !== fieldValueText(afterFields[key]));

  if (changed.length === 0) {
    return <p className="text-small text-muted-foreground">No field differences between these two submissions.</p>;
  }

  return (
    <div className="space-y-3">
      {changed.map((key) => (
        <div key={key} className="rounded-md border border-border p-3">
          <p className="text-caption font-medium uppercase tracking-wide text-muted-foreground">{key}</p>
          <p className="mt-1 break-words text-small text-error line-through">{fieldValueText(beforeFields[key])}</p>
          <p className="mt-1 break-words text-small text-success">{fieldValueText(afterFields[key])}</p>
        </div>
      ))}
    </div>
  );
}

/**
 * The editorial history for one content item: every submission snapshot and every reviewer action
 * (with feedback comments, pinned to the snapshot they were actually given against), plus a simple
 * two-snapshot comparison. Available to the content's owner and anyone who can review it — the
 * same access rule the backend enforces.
 */
export function AidxRevisionHistory({ contentType, id }: { contentType: AidxWorkflowContentType; id: string }) {
  const { getToken } = useAuth();
  const [revisions, setRevisions] = React.useState<AidxRevisionSummary[]>();
  const [entries, setEntries] = React.useState<AidxAuditEntry[]>();
  const [error, setError] = React.useState<string | null>(null);
  const [compareIndexes, setCompareIndexes] = React.useState<[number, number] | null>(null);

  React.useEffect(() => {
    let cancelled = false;
    async function load() {
      setError(null);
      try {
        const token = await getToken();
        const [revisionList, auditList] = await Promise.all([
          listRevisions(token, contentType, id),
          listAuditEntries(token, contentType, id),
        ]);
        if (cancelled) return;
        setRevisions(revisionList);
        setEntries(auditList);
      } catch (caught) {
        if (!cancelled) setError(formError(caught));
      }
    }
    void load();
    return () => {
      cancelled = true;
    };
  }, [contentType, id, getToken]);

  if (error) {
    return (
      <Alert tone="error" title="Couldn't load the history">
        {error}
      </Alert>
    );
  }

  if (!revisions || !entries) {
    return <Skeleton className="h-32 w-full" />;
  }

  const entriesBySnapshot = new Map<string, AidxAuditEntry[]>();
  for (const entry of entries) {
    if (!entry.snapshotId) continue;
    const list = entriesBySnapshot.get(entry.snapshotId) ?? [];
    list.push(entry);
    entriesBySnapshot.set(entry.snapshotId, list);
  }

  return (
    <div className="space-y-4">
      {revisions.length === 0 ? (
        <p className="text-small text-muted-foreground">No submissions yet.</p>
      ) : (
        <ul className="space-y-3">
          {revisions.map((revision, index) => (
            <li key={revision.id} className="rounded-md border border-border p-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <p className="text-small font-medium text-foreground">
                  Submission #{revision.snapshotNumber} — {formatDateTime(revision.createdAt)}
                </p>
                {index > 0 ? (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => setCompareIndexes(compareIndexes?.[1] === index ? null : [index - 1, index])}
                  >
                    {compareIndexes?.[1] === index ? "Hide comparison" : "Compare with previous"}
                  </Button>
                ) : null}
              </div>
              {(entriesBySnapshot.get(revision.id) ?? []).map((entry) => (
                <div key={entry.id} className="mt-2 border-l-2 border-border pl-3">
                  <p className="text-caption text-muted-foreground">
                    {actionLabel(entry.action)} by {entry.performedByName} · {formatDateTime(entry.createdAt)}
                  </p>
                  {entry.comment ? <p className="mt-1 text-small text-foreground">{entry.comment}</p> : null}
                </div>
              ))}
              {compareIndexes && compareIndexes[1] === index ? (
                <Card className="mt-3 space-y-3 p-3">
                  <h3 className="text-caption font-medium uppercase tracking-wide text-muted-foreground">
                    What changed since submission #{revisions[compareIndexes[0]].snapshotNumber}
                  </h3>
                  <SnapshotDiff before={revisions[compareIndexes[0]]} after={revisions[compareIndexes[1]]} />
                </Card>
              ) : null}
            </li>
          ))}
        </ul>
      )}

      {entries.filter((entry) => !entry.snapshotId).length > 0 ? (
        <div className="space-y-2">
          <h3 className="text-caption font-medium uppercase tracking-wide text-muted-foreground">Other activity</h3>
          {entries
            .filter((entry) => !entry.snapshotId)
            .map((entry) => (
              <p key={entry.id} className="text-caption text-muted-foreground">
                {actionLabel(entry.action)} by {entry.performedByName} · {formatDateTime(entry.createdAt)}
              </p>
            ))}
        </div>
      ) : null}
    </div>
  );
}
