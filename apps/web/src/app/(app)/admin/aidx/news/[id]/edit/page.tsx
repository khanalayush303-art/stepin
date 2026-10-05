"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { SearchX } from "lucide-react";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { describeAdminError, type AdminOpportunityError } from "@/lib/aidx/admin";
import { formatDate } from "@/lib/utils";
import {
  archiveAdminNews,
  deleteAdminNews,
  getAdminNews,
  publishAdminNews,
  updateAdminNews,
  type AdminNewsDetail,
  type NewsInput,
} from "@/lib/aidx/news-cms";
import { listAdminPeople, type AdminPersonSummary } from "@/lib/aidx/people-cms";
import { ADMIN_NAV } from "../../../../_nav";
import { ConfirmDialog } from "../../../opportunities/_components/confirm-dialog";
import { ProjectStatusBadge } from "../../../projects/_components/project-status-badge";
import { NewsForm } from "../../_components/news-form";

type Pending = "publish" | "archive" | "delete" | null;

function toInput(item: AdminNewsDetail): NewsInput {
  return {
    title: item.title,
    slug: item.slug,
    summary: item.summary,
    body: item.body,
    authorResearcherId: item.authorResearcherId,
  };
}

export default function EditAidxNewsPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const router = useRouter();
  const { getToken } = useAuth();

  const [item, setItem] = React.useState<AdminNewsDetail | null>(null);
  const [value, setValue] = React.useState<NewsInput | null>(null);
  const [researchers, setResearchers] = React.useState<AdminPersonSummary[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<AdminOpportunityError | null>(null);
  const [saving, setSaving] = React.useState(false);
  const [saveError, setSaveError] = React.useState<AdminOpportunityError | null>(null);
  const [notice, setNotice] = React.useState<string | null>(null);
  const [pending, setPending] = React.useState<Pending>(null);
  const [actionBusy, setActionBusy] = React.useState(false);
  const [reloadKey, setReloadKey] = React.useState(0);

  React.useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      setLoadError(null);
      try {
        const token = await getToken();
        const [news, people] = await Promise.all([
          getAdminNews(token, id),
          listAdminPeople(token, { pageSize: 100 }).then((p) => p.items).catch(() => [] as AdminPersonSummary[]),
        ]);
        if (cancelled) return;
        setItem(news);
        setValue(toInput(news));
        setResearchers(people);
      } catch (caught) {
        if (!cancelled) setLoadError(describeAdminError(caught));
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    void load();
    return () => {
      cancelled = true;
    };
  }, [id, getToken, reloadKey]);

  async function onSave() {
    if (!value) return;
    setSaving(true);
    setSaveError(null);
    setNotice(null);
    try {
      const token = await getToken();
      await updateAdminNews(token, id, value);
      setNotice("Changes saved.");
      setReloadKey((key) => key + 1);
    } catch (caught) {
      setSaveError(describeAdminError(caught));
    } finally {
      setSaving(false);
    }
  }

  async function runAction(kind: Exclude<Pending, null>) {
    setActionBusy(true);
    setSaveError(null);
    setNotice(null);
    try {
      const token = await getToken();
      if (kind === "delete") {
        await deleteAdminNews(token, id);
        router.push("/admin/aidx/news");
        return;
      }
      if (kind === "publish") {
        await publishAdminNews(token, id);
        setNotice("Published. The story is now on the public news page.");
      } else {
        await archiveAdminNews(token, id);
        setNotice("Archived. The story is no longer public.");
      }
      setPending(null);
      setReloadKey((key) => key + 1);
    } catch (caught) {
      setSaveError(describeAdminError(caught));
      setPending(null);
    } finally {
      setActionBusy(false);
    }
  }

  const status = item?.status;
  const canPublish = status === "Draft" || status === "Archived";
  const canArchive = status === "Published";
  const canDelete = status === "Draft";
  const fieldErrors = saveError?.kind === "validation" ? saveError.fields : {};

  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="max-w-3xl space-y-6">
        <nav aria-label="Breadcrumb" className="text-small text-muted-foreground">
          <Link href="/admin/aidx/news" className="underline-offset-4 hover:underline">
            News
          </Link>
          <span aria-hidden="true"> / </span>
          <span className="text-foreground">Edit</span>
        </nav>

        {loading ? (
          <div className="space-y-4" aria-busy="true" aria-label="Loading story">
            <Skeleton className="h-8 w-2/3" />
            <Skeleton className="h-64 w-full" />
          </div>
        ) : null}

        {!loading && loadError?.kind === "not-found" ? (
          <EmptyState
            icon={<SearchX aria-hidden="true" className="size-6" />}
            title="Story not found"
            description={loadError.message}
            action={
              <Button asChild variant="outline">
                <Link href="/admin/aidx/news">Back to news</Link>
              </Button>
            }
          />
        ) : null}

        {!loading && loadError && loadError.kind !== "not-found" ? (
          <Alert tone={loadError.kind === "unauthenticated" ? "warning" : "error"} title="Unable to load this story">
            <p>{loadError.message}</p>
            {loadError.kind !== "forbidden" ? (
              <Button variant="outline" className="mt-3" onClick={() => setReloadKey((key) => key + 1)}>
                Try again
              </Button>
            ) : null}
          </Alert>
        ) : null}

        {!loading && item && value ? (
          <>
            <div className="space-y-1">
              <h1 className="break-words text-h2 text-foreground">{item.title}</h1>
              <div className="flex flex-wrap items-center gap-3 text-small text-muted-foreground">
                <ProjectStatusBadge status={item.status} />
                {item.publishedAt ? <span>Published {formatDate(item.publishedAt)}</span> : null}
                <span className="break-all">Public page: /aidx/news/{item.slug}</span>
              </div>
            </div>

            {notice ? (
              <Alert tone="success" title="Done">
                {notice}
              </Alert>
            ) : null}

            {saveError ? (
              <Alert tone="error" title={saveError.kind === "validation" ? "Check the form" : "Couldn't complete that change"}>
                {saveError.message}
              </Alert>
            ) : null}

            <Card className="space-y-4 p-5">
              <h2 className="text-h4 text-foreground">Publishing</h2>
              <p className="text-small text-muted-foreground">
                {status === "Published"
                  ? "This story is public. Archiving it removes it from the news page, and its link stops working."
                  : status === "Archived"
                    ? "This story is archived and private. Publishing it again keeps its original publish date."
                    : "This story is a draft and is not public. Publish it when it is ready."}
              </p>
              <div className="flex flex-wrap gap-3">
                {canPublish ? (
                  <Button onClick={() => setPending("publish")} disabled={actionBusy || saving}>
                    Publish
                  </Button>
                ) : null}
                {canArchive ? (
                  <Button variant="outline" onClick={() => setPending("archive")} disabled={actionBusy || saving}>
                    Archive
                  </Button>
                ) : null}
                {canDelete ? (
                  <Button variant="outline" onClick={() => setPending("delete")} disabled={actionBusy || saving}>
                    Delete draft
                  </Button>
                ) : null}
              </div>
              {!canDelete ? (
                <p className="text-caption text-muted-foreground">
                  Published and archived stories cannot be deleted, because their links may already be shared. Archive
                  takes them down instead.
                </p>
              ) : null}
            </Card>

            <form
              onSubmit={(event) => {
                event.preventDefault();
                void onSave();
              }}
              className="space-y-6"
            >
              <NewsForm
                value={value}
                onChange={setValue}
                fieldErrors={fieldErrors}
                disabled={saving || actionBusy}
                researchers={researchers}
              />
              <div className="flex justify-end">
                <Button type="submit" size="lg" loading={saving} loadingText="Saving…" disabled={actionBusy}>
                  Save changes
                </Button>
              </div>
            </form>
          </>
        ) : null}
      </div>

      <ConfirmDialog
        open={pending === "publish"}
        onOpenChange={(open) => !open && setPending(null)}
        title="Publish story?"
        description={`The story will appear on the AIDX Lab news page at /aidx/news/${item?.slug ?? ""}.`}
        confirmLabel="Publish"
        busy={actionBusy}
        onConfirm={() => void runAction("publish")}
      />
      <ConfirmDialog
        open={pending === "archive"}
        onOpenChange={(open) => !open && setPending(null)}
        title="Archive story?"
        description="It will be removed from the public news page, and its link will stop working. You can publish it again later, and it keeps its original publish date."
        confirmLabel="Archive"
        busy={actionBusy}
        onConfirm={() => void runAction("archive")}
      />
      <ConfirmDialog
        open={pending === "delete"}
        onOpenChange={(open) => !open && setPending(null)}
        title="Delete draft story?"
        description="This permanently removes the draft. This cannot be undone."
        confirmLabel="Delete draft"
        destructive
        busy={actionBusy}
        onConfirm={() => void runAction("delete")}
      />
    </DashboardShell>
  );
}
