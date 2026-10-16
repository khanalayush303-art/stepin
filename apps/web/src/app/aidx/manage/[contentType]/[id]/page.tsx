"use client";

import * as React from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { AidxRevisionHistory } from "@/components/aidx/aidx-revision-history";
import { AidxRichText } from "@/components/aidx/aidx-rich-text";
import { AidxWorkflowStatusBadge } from "@/components/aidx/aidx-workflow-status-badge";
import { getAuthoringEvent } from "@/lib/aidx/authoring";
import { getAuthoringNews } from "@/lib/aidx/authoring";
import { getAuthoringProject } from "@/lib/aidx/authoring";
import { getAuthoringPublication } from "@/lib/aidx/authoring";
import { formError } from "@/lib/auth/client";
import {
  approve,
  archiveContent,
  currentVersion,
  requestChanges,
  startReview,
  type AidxWorkflowContentType,
} from "@/lib/aidx/workflow";

interface ReviewView {
  title: string;
  status: string;
  blocks: { heading: string; text: string }[];
}

async function loadReviewView(token: string | null, contentType: AidxWorkflowContentType, id: string): Promise<ReviewView> {
  switch (contentType) {
    case "project": {
      const p = await getAuthoringProject(token, id);
      return {
        title: p.title,
        status: p.status,
        blocks: [
          { heading: "Summary", text: p.shortDescription },
          { heading: "Description", text: p.description },
          ...(p.outcomes ? [{ heading: "Outcomes", text: p.outcomes }] : []),
        ],
      };
    }
    case "news": {
      const n = await getAuthoringNews(token, id);
      return { title: n.title, status: n.status, blocks: [{ heading: "Summary", text: n.summary }, { heading: "Body", text: n.body }] };
    }
    case "event": {
      const e = await getAuthoringEvent(token, id);
      return { title: e.title, status: e.status, blocks: [{ heading: "Description", text: e.description }] };
    }
    case "publication": {
      const pub = await getAuthoringPublication(token, id);
      return { title: pub.title, status: pub.status, blocks: pub.abstract ? [{ heading: "Abstract", text: pub.abstract }] : [] };
    }
  }
}

export default function ManageReviewDetailPage() {
  const params = useParams<{ contentType: string; id: string }>();
  const contentType = params.contentType as AidxWorkflowContentType;
  const id = params.id;
  const { getToken } = useAuth();

  const [view, setView] = React.useState<ReviewView | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string | null>(null);
  const [actionError, setActionError] = React.useState<string | null>(null);
  const [actionNotice, setActionNotice] = React.useState<string | null>(null);
  const [busy, setBusy] = React.useState(false);
  const [comment, setComment] = React.useState("");
  const [reloadKey, setReloadKey] = React.useState(0);

  React.useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      setLoadError(null);
      try {
        const token = await getToken();
        const result = await loadReviewView(token, contentType, id);
        if (!cancelled) setView(result);
      } catch (caught) {
        if (!cancelled) setLoadError(formError(caught));
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    void load();
    return () => {
      cancelled = true;
    };
  }, [contentType, id, getToken, reloadKey]);

  async function run(action: "start-review" | "approve" | "archive" | "request-changes") {
    setBusy(true);
    setActionError(null);
    setActionNotice(null);
    try {
      const token = await getToken();
      const version = await currentVersion(token, contentType, id);
      if (action === "start-review") {
        await startReview(token, contentType, id, version);
        setActionNotice("Marked as under review.");
      } else if (action === "approve") {
        await approve(token, contentType, id, version);
        setActionNotice("Approved and published.");
      } else if (action === "archive") {
        await archiveContent(token, contentType, id, version);
        setActionNotice("Archived.");
      } else {
        if (!comment.trim()) {
          setActionError("Add a comment explaining what needs to change.");
          setBusy(false);
          return;
        }
        await requestChanges(token, contentType, id, version, comment.trim());
        setActionNotice("Changes requested.");
        setComment("");
      }
      setReloadKey((key) => key + 1);
    } catch (caught) {
      setActionError(formError(caught));
    } finally {
      setBusy(false);
    }
  }

  const status = view?.status;

  return (
    <div className="container-page max-w-3xl space-y-6 py-10">
      <nav aria-label="Breadcrumb" className="text-small text-muted-foreground">
        <Link href="/aidx/manage" className="underline-offset-4 hover:underline">
          Manage AIDX
        </Link>
        <span aria-hidden="true"> / </span>
        <span className="text-foreground">Review</span>
      </nav>

      {loading ? <Skeleton className="h-64 w-full" /> : null}

      {!loading && loadError ? (
        <Alert tone="error" title="Couldn't load this submission">
          {loadError}
        </Alert>
      ) : null}

      {!loading && view ? (
        <>
          <div className="space-y-1">
            <h1 className="break-words text-h2 text-foreground">{view.title}</h1>
            <AidxWorkflowStatusBadge status={view.status} />
          </div>

          {actionNotice ? (
            <Alert tone="success" title="Done">
              {actionNotice}
            </Alert>
          ) : null}
          {actionError ? (
            <Alert tone="error" title="That action didn't go through">
              {actionError}
            </Alert>
          ) : null}

          <Card className="space-y-4 p-5">
            {view.blocks.map((block) => (
              <div key={block.heading} className="space-y-2">
                <h2 className="text-h4 text-foreground">{block.heading}</h2>
                <AidxRichText text={block.text} className="space-y-3" />
              </div>
            ))}
          </Card>

          <Card className="space-y-3 p-5">
            <h2 className="text-h4 text-foreground">Review actions</h2>
            <div className="flex flex-wrap gap-3">
              {status === "Submitted" ? (
                <Button variant="outline" loading={busy} onClick={() => run("start-review")}>
                  Start review
                </Button>
              ) : null}
              {(status === "Submitted" || status === "UnderReview") ? (
                <Button loading={busy} onClick={() => run("approve")}>
                  Approve
                </Button>
              ) : null}
              {status === "Published" ? (
                <Button variant="outline" loading={busy} onClick={() => run("archive")}>
                  Archive
                </Button>
              ) : null}
            </div>
            {status === "Submitted" || status === "UnderReview" ? (
              <div className="flex flex-wrap items-center gap-2 pt-2">
                <Textarea
                  placeholder="What needs to change?"
                  className="min-h-[2.5rem] flex-1"
                  value={comment}
                  onChange={(event) => setComment(event.target.value)}
                />
                <Button variant="outline" loading={busy} onClick={() => run("request-changes")}>
                  Request changes
                </Button>
              </div>
            ) : null}
          </Card>

          <Card className="space-y-3 p-5">
            <h2 className="text-h4 text-foreground">History</h2>
            <AidxRevisionHistory contentType={contentType} id={id} />
          </Card>
        </>
      ) : null}
    </div>
  );
}
