"use client";

import * as React from "react";
import { useAuth } from "@clerk/nextjs";
import { Inbox } from "lucide-react";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { AidxStatusBadge } from "@/components/aidx/aidx-status-badge";
import { apiClient, formError } from "@/lib/auth/client";
import type { AidxWorkspaceItem, AidxWorkspaceSummary } from "@/lib/aidx/workspace-types";

const STAT_CARDS: { key: keyof AidxWorkspaceSummary; label: string }[] = [
  { key: "myDrafts", label: "Drafts" },
  { key: "mySubmitted", label: "Submitted" },
  { key: "myUnderReview", label: "Under review" },
  { key: "myChangesRequested", label: "Changes requested" },
  { key: "myPublished", label: "Published" },
  { key: "myArchived", label: "Archived" },
];

export default function AidxWorkspacePage() {
  const { getToken } = useAuth();
  const [summary, setSummary] = React.useState<AidxWorkspaceSummary>();
  const [items, setItems] = React.useState<AidxWorkspaceItem[]>();
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [actionError, setActionError] = React.useState<string>();
  const [actingOn, setActingOn] = React.useState<string>();

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      const [summaryResult, itemsResult] = await Promise.all([
        apiClient.get<AidxWorkspaceSummary>("/api/v1/aidx/workspace/summary", token),
        apiClient.get<AidxWorkspaceItem[]>("/api/v1/aidx/workspace/items", token),
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

  async function withdraw(item: AidxWorkspaceItem) {
    setActingOn(item.id);
    setActionError(undefined);
    try {
      const token = await getToken();
      const detail = await apiClient.get<{ version: string }>(
        `/api/v1/aidx/content/${item.contentType.toLowerCase()}/${item.id}/begin-edit`,
        token
      );
      await apiClient.post(`/api/v1/aidx/content/${item.contentType.toLowerCase()}/${item.id}/withdraw`, token, {
        expectedVersion: detail.version,
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
        <Alert tone="error" title="Couldn't load your workspace.">
          {loadError}
        </Alert>
      </div>
    );
  }

  return (
    <div className="container-page space-y-8 py-10">
      <div className="space-y-1">
        <h1 className="text-h2 text-foreground">My AIDX workspace</h1>
        <p className="text-body text-muted-foreground">
          Your own research content, submitted for review or already published. Creating brand-new
          content currently goes through the AIDX admin team — this page tracks and submits what
          already exists.
        </p>
      </div>

      <ul className="grid gap-4 sm:grid-cols-3 lg:grid-cols-6">
        {STAT_CARDS.map(({ key, label }) => (
          <li key={key}>
            <Card className="p-4">
              <p className="text-caption text-muted-foreground">{label}</p>
              <p className="text-h3 text-foreground">{summary?.[key] ?? 0}</p>
            </Card>
          </li>
        ))}
      </ul>

      {actionError ? (
        <Alert tone="error" title="That action didn't go through.">
          {actionError}
        </Alert>
      ) : null}

      {items && items.length > 0 ? (
        <Card className="divide-y divide-border p-0">
          {items.map((item) => (
            <div key={item.id} className="flex flex-wrap items-center gap-4 p-4">
              <div className="min-w-0 flex-1">
                <p className="text-small font-medium text-foreground">{item.title}</p>
                <p className="text-caption text-muted-foreground">
                  {item.contentType}
                  {item.revisionOfId ? " · revision in progress" : ""}
                </p>
              </div>
              <AidxStatusBadge status={item.status} />
              {item.status === "Submitted" || item.status === "UnderReview" ? (
                <Button
                  variant="outline"
                  size="sm"
                  loading={actingOn === item.id}
                  onClick={() => withdraw(item)}
                >
                  Withdraw
                </Button>
              ) : null}
            </div>
          ))}
        </Card>
      ) : (
        <EmptyState
          icon={<Inbox className="size-6" />}
          title="No content yet"
          description="Nothing has been created under your name yet. Once the AIDX team adds content attributed to you, it will show up here for you to submit and track."
        />
      )}
    </div>
  );
}
