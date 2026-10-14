"use client";

import * as React from "react";
import { useAuth } from "@clerk/nextjs";
import { CheckCircle2 } from "lucide-react";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { AidxStatusBadge } from "@/components/aidx/aidx-status-badge";
import { apiClient, formError } from "@/lib/auth/client";
import type { AidxManageSummary, AidxWorkspaceItem } from "@/lib/aidx/workspace-types";

const STAT_CARDS: { key: keyof AidxManageSummary; label: string }[] = [
  { key: "projectsAwaitingReview", label: "Projects" },
  { key: "newsAwaitingReview", label: "News" },
  { key: "eventsAwaitingReview", label: "Events" },
  { key: "publicationsAwaitingReview", label: "Publications" },
];

export default function AidxManagePage() {
  const { getToken } = useAuth();
  const [summary, setSummary] = React.useState<AidxManageSummary>();
  const [items, setItems] = React.useState<AidxWorkspaceItem[]>();
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [actionError, setActionError] = React.useState<string>();
  const [actingOn, setActingOn] = React.useState<string>();
  const [comments, setComments] = React.useState<Record<string, string>>({});

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      const [summaryResult, itemsResult] = await Promise.all([
        apiClient.get<AidxManageSummary>("/api/v1/aidx/manage/summary", token),
        apiClient.get<AidxWorkspaceItem[]>("/api/v1/aidx/manage/queue", token),
      ]);
      setSummary(summaryResult);
      setItems(itemsResult);
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

  async function currentVersion(item: AidxWorkspaceItem, token: string | null) {
    const detail = await apiClient.get<{ version: string }>(
      `/api/v1/aidx/content/${item.contentType.toLowerCase()}/${item.id}/begin-edit`,
      token
    );
    return detail.version;
  }

  async function approve(item: AidxWorkspaceItem) {
    setActingOn(item.id);
    setActionError(undefined);
    try {
      const token = await getToken();
      const version = await currentVersion(item, token);
      await apiClient.post(`/api/v1/aidx/content/${item.contentType.toLowerCase()}/${item.id}/approve`, token, {
        expectedVersion: version,
      });
      await load();
    } catch (error) {
      setActionError(formError(error));
    } finally {
      setActingOn(undefined);
    }
  }

  async function requestChanges(item: AidxWorkspaceItem) {
    const comment = comments[item.id]?.trim();
    if (!comment) {
      setActionError("Add a comment explaining what needs to change before requesting changes.");
      return;
    }

    setActingOn(item.id);
    setActionError(undefined);
    try {
      const token = await getToken();
      const version = await currentVersion(item, token);
      await apiClient.post(`/api/v1/aidx/content/${item.contentType.toLowerCase()}/${item.id}/request-changes`, token, {
        expectedVersion: version,
        comment,
      });
      await load();
    } catch (error) {
      setActionError(formError(error));
    } finally {
      setActingOn(undefined);
    }
  }

  if (loading) {
    return (
      <div className="container-page space-y-6 py-10">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-40 w-full" />
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="container-page py-10">
        <Alert tone="error" title="Couldn't load the review queue.">
          {loadError}
        </Alert>
      </div>
    );
  }

  return (
    <div className="container-page space-y-8 py-10">
      <div className="space-y-1">
        <h1 className="text-h2 text-foreground">Manage AIDX</h1>
        <p className="text-body text-muted-foreground">Content submitted or under review across the lab.</p>
      </div>

      <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {STAT_CARDS.map(({ key, label }) => {
          const value = summary?.[key];
          return (
            <li key={key}>
              <Card className="p-4">
                <p className="text-caption text-muted-foreground">{label}</p>
                <p className="text-h3 text-foreground">{value ?? "—"}</p>
                {value === null ? <p className="text-caption text-muted-foreground">Not in your scope</p> : null}
              </Card>
            </li>
          );
        })}
      </ul>

      {actionError ? (
        <Alert tone="error" title="That action didn't go through.">
          {actionError}
        </Alert>
      ) : null}

      {items && items.length > 0 ? (
        <Card className="divide-y divide-border p-0">
          {items.map((item) => (
            <div key={item.id} className="space-y-3 p-4">
              <div className="flex flex-wrap items-center gap-4">
                <div className="min-w-0 flex-1">
                  <p className="text-small font-medium text-foreground">{item.title}</p>
                  <p className="text-caption text-muted-foreground">
                    {item.contentType}
                    {item.revisionOfId ? " · revision" : ""}
                  </p>
                </div>
                <AidxStatusBadge status={item.status} />
                <Button variant="primary" size="sm" loading={actingOn === item.id} onClick={() => approve(item)}>
                  Approve
                </Button>
              </div>
              <div className="flex flex-wrap items-center gap-2">
                <Textarea
                  placeholder="What needs to change?"
                  className="min-h-[2.5rem] flex-1"
                  value={comments[item.id] ?? ""}
                  onChange={(e) => setComments((prev) => ({ ...prev, [item.id]: e.target.value }))}
                />
                <Button
                  variant="outline"
                  size="sm"
                  loading={actingOn === item.id}
                  onClick={() => requestChanges(item)}
                >
                  Request changes
                </Button>
              </div>
            </div>
          ))}
        </Card>
      ) : (
        <EmptyState
          icon={<CheckCircle2 className="size-6" />}
          title="Nothing is waiting for review"
          description="Submitted and under-review content within your scope will show up here."
        />
      )}
    </div>
  );
}
