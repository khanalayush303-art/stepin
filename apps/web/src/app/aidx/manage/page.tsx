"use client";

import * as React from "react";
import Link from "next/link";
import { useAuth } from "@clerk/nextjs";
import { CheckCircle2 } from "lucide-react";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { AidxWorkflowStatusBadge } from "@/components/aidx/aidx-workflow-status-badge";
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
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Manage AIDX</h1>
          <p className="text-body text-muted-foreground">Content submitted or under review across the lab.</p>
        </div>
        <Button variant="outline" asChild>
          <Link href="/aidx/manage/memberships">Memberships</Link>
        </Button>
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

      {items && items.length > 0 ? (
        <Card className="divide-y divide-border p-0">
          {items.map((item) => (
            <div key={item.id} className="flex flex-wrap items-center gap-4 p-4">
              <div className="min-w-0 flex-1">
                <p className="text-small font-medium text-foreground">{item.title}</p>
                <p className="text-caption text-muted-foreground">
                  {item.contentType}
                  {item.revisionOfId ? " · revision" : ""}
                </p>
              </div>
              <AidxWorkflowStatusBadge status={item.status} />
              <Button variant="primary" size="sm" asChild>
                <Link href={`/aidx/manage/${item.contentType.toLowerCase()}/${item.id}`}>Review</Link>
              </Button>
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
