"use client";

import * as React from "react";
import Link from "next/link";
import { useAuth } from "@clerk/nextjs";
import { Inbox } from "lucide-react";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { AidxWorkflowStatusBadge } from "@/components/aidx/aidx-workflow-status-badge";
import { apiClient, formError } from "@/lib/auth/client";
import { currentVersion, withdraw as withdrawContent, workspaceEditPath, type AidxWorkflowContentType } from "@/lib/aidx/workflow";
import type { AidxWorkspaceItem, AidxWorkspaceSummary } from "@/lib/aidx/workspace-types";

const STAT_CARDS: { key: keyof AidxWorkspaceSummary; label: string }[] = [
  { key: "myDrafts", label: "Drafts" },
  { key: "mySubmitted", label: "Submitted" },
  { key: "myUnderReview", label: "Under review" },
  { key: "myChangesRequested", label: "Changes requested" },
  { key: "myPublished", label: "Published" },
  { key: "myArchived", label: "Archived" },
];

const NEW_LINKS: { contentType: AidxWorkflowContentType; label: string; href: string }[] = [
  { contentType: "project", label: "New project", href: "/aidx/workspace/projects/new" },
  { contentType: "news", label: "New story", href: "/aidx/workspace/news/new" },
  { contentType: "event", label: "New event", href: "/aidx/workspace/events/new" },
  { contentType: "publication", label: "New publication", href: "/aidx/workspace/publications/new" },
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

  async function onWithdraw(item: AidxWorkspaceItem) {
    setActingOn(item.id);
    setActionError(undefined);
    try {
      const token = await getToken();
      const type = item.contentType.toLowerCase() as AidxWorkflowContentType;
      const version = await currentVersion(token, type, item.id);
      await withdrawContent(token, type, item.id, version);
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
          Your own research content, submitted for review or already published.
        </p>
      </div>

      <Card className="space-y-3 p-5">
        <h2 className="text-h4 text-foreground">Start something new</h2>
        <div className="flex flex-wrap gap-3">
          {NEW_LINKS.map((link) => (
            <Button key={link.contentType} variant="outline" asChild>
              <Link href={link.href}>{link.label}</Link>
            </Button>
          ))}
        </div>
      </Card>

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
                <Link
                  href={workspaceEditPath(item.contentType, item.id)}
                  className="font-medium text-small text-foreground underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                >
                  {item.title}
                </Link>
                <p className="text-caption text-muted-foreground">
                  {item.contentType}
                  {item.revisionOfId ? " · revision in progress" : ""}
                </p>
              </div>
              <AidxWorkflowStatusBadge status={item.status} />
              {item.contentType === "Project" ? (
                <Button variant="ghost" size="sm" asChild>
                  <Link href={`/aidx/workspace/enquiries/${item.id}`}>Enquiries</Link>
                </Button>
              ) : null}
              {item.contentType === "Event" ? (
                <Button variant="ghost" size="sm" asChild>
                  <Link href={`/aidx/workspace/registrations/${item.id}`}>Registrations</Link>
                </Button>
              ) : null}
              <Button variant="outline" size="sm" asChild>
                <Link href={workspaceEditPath(item.contentType, item.id)}>Edit</Link>
              </Button>
              {item.status === "Submitted" || item.status === "UnderReview" ? (
                <Button variant="outline" size="sm" loading={actingOn === item.id} onClick={() => onWithdraw(item)}>
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
          description="Start a project, story, event, or publication above — it is saved as your own private draft until you submit it for review."
        />
      )}
    </div>
  );
}
