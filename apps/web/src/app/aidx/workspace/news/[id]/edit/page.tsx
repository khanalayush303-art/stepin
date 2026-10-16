"use client";

import * as React from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { Eye, EyeOff, SearchX } from "lucide-react";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { AidxImageUpload } from "@/components/aidx/aidx-image-upload";
import { AidxRevisionHistory } from "@/components/aidx/aidx-revision-history";
import { AidxRichText } from "@/components/aidx/aidx-rich-text";
import { AidxSaveStatus, type AidxSaveState } from "@/components/aidx/aidx-save-status";
import { AidxWorkflowStatusBadge } from "@/components/aidx/aidx-workflow-status-badge";
import { NewsForm } from "@/app/(app)/admin/aidx/news/_components/news-form";
import { describeAdminError, type AdminOpportunityError } from "@/lib/aidx/admin";
import { getAuthoringNews, updateAuthoringNews, uploadAuthoringNewsImage } from "@/lib/aidx/authoring";
import { newsImageUrl, type AdminNewsDetail, type NewsInput } from "@/lib/aidx/news-cms";
import { listAdminPeople, type AdminPersonSummary } from "@/lib/aidx/people-cms";
import { listAuditEntries, submitForReview, withdraw, type AidxAuditEntry } from "@/lib/aidx/workflow";
import { useUnsavedChangesGuard } from "@/lib/aidx/use-unsaved-changes-guard";

function toInput(item: AdminNewsDetail): NewsInput {
  return {
    title: item.title,
    slug: item.slug,
    summary: item.summary,
    body: item.body,
    authorResearcherId: item.authorResearcherId,
    expectedVersion: item.version,
  };
}

export default function EditWorkspaceNewsPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const { getToken } = useAuth();

  const [loaded, setLoaded] = React.useState<AdminNewsDetail | null>(null);
  const [value, setValue] = React.useState<NewsInput | null>(null);
  const [dirty, setDirty] = React.useState(false);
  const [researchers, setResearchers] = React.useState<AdminPersonSummary[]>([]);
  const [auditEntries, setAuditEntries] = React.useState<AidxAuditEntry[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<AdminOpportunityError | null>(null);
  const [saveState, setSaveState] = React.useState<AidxSaveState>("idle");
  const [saveError, setSaveError] = React.useState<AdminOpportunityError | null>(null);
  const [actionBusy, setActionBusy] = React.useState(false);
  const [actionError, setActionError] = React.useState<string | null>(null);
  const [showPreview, setShowPreview] = React.useState(false);
  const [reloadKey, setReloadKey] = React.useState(0);

  useUnsavedChangesGuard(dirty);

  React.useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      setLoadError(null);
      try {
        const token = await getToken();
        const [item, people, audit] = await Promise.all([
          getAuthoringNews(token, id),
          listAdminPeople(token, { pageSize: 200 }).then((p) => p.items).catch(() => []),
          listAuditEntries(token, "news", id).catch(() => [] as AidxAuditEntry[]),
        ]);
        if (cancelled) return;
        setLoaded(item);
        setValue(toInput(item));
        setDirty(false);
        setResearchers(people);
        setAuditEntries(audit);
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
    setSaveState("saving");
    setSaveError(null);
    try {
      const token = await getToken();
      await updateAuthoringNews(token, id, value);
      setSaveState("saved");
      setDirty(false);
      setReloadKey((key) => key + 1);
    } catch (caught) {
      setSaveError(describeAdminError(caught));
      setSaveState("error");
    }
  }

  async function onSubmit() {
    if (!loaded) return;
    setActionBusy(true);
    setActionError(null);
    try {
      const token = await getToken();
      await submitForReview(token, "news", id, loaded.version ?? "");
      setReloadKey((key) => key + 1);
    } catch (caught) {
      setActionError(describeAdminError(caught).message);
    } finally {
      setActionBusy(false);
    }
  }

  async function onWithdraw() {
    if (!loaded) return;
    setActionBusy(true);
    setActionError(null);
    try {
      const token = await getToken();
      await withdraw(token, "news", id, loaded.version ?? "");
      setReloadKey((key) => key + 1);
    } catch (caught) {
      setActionError(describeAdminError(caught).message);
    } finally {
      setActionBusy(false);
    }
  }

  const fieldErrors = saveError?.kind === "validation" ? saveError.fields : {};
  const status = loaded?.status;
  const isEditable = status === "Draft" || status === "ChangesRequested" || status === "Published";
  const canSubmit = status === "Draft" || status === "ChangesRequested";
  const canWithdraw = status === "Submitted" || status === "UnderReview";
  const latestChangesRequested = [...auditEntries].reverse().find((entry) => entry.action === "ChangesRequested");

  return (
    <div className="container-page max-w-3xl space-y-6 py-10">
      <nav aria-label="Breadcrumb" className="text-small text-muted-foreground">
        <Link href="/aidx/workspace" className="underline-offset-4 hover:underline">
          My AIDX workspace
        </Link>
        <span aria-hidden="true"> / </span>
        <span className="text-foreground">Edit story</span>
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
              <Link href="/aidx/workspace">Back to my workspace</Link>
            </Button>
          }
        />
      ) : null}

      {!loading && loadError && loadError.kind !== "not-found" ? (
        <Alert tone={loadError.kind === "unauthenticated" ? "warning" : "error"} title="Unable to load this story">
          {loadError.message}
        </Alert>
      ) : null}

      {!loading && loaded && value ? (
        <>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="space-y-1">
              <h1 className="break-words text-h2 text-foreground">{loaded.title}</h1>
              <AidxWorkflowStatusBadge status={loaded.status} />
            </div>
            <AidxSaveStatus state={saveState} errorMessage={saveError?.message} />
          </div>

          {!isEditable ? (
            <Alert tone="warning" title="This is read-only right now">
              This story is {loaded.status === "Submitted" ? "submitted" : "under review"}. Withdraw it first if you
              need to make changes.
            </Alert>
          ) : null}

          {latestChangesRequested && status === "ChangesRequested" ? (
            <Alert tone="warning" title="Changes were requested">
              <p>{latestChangesRequested.comment}</p>
              <p className="mt-1 text-caption text-muted-foreground">
                From {latestChangesRequested.performedByName} · {new Date(latestChangesRequested.createdAt).toLocaleString()}
              </p>
            </Alert>
          ) : null}

          {saveState === "saved" ? (
            <Alert tone="success" title="Saved">
              {loaded.revisionOfId
                ? "Saved as a pending revision. The published story is unchanged until this revision is reviewed and approved."
                : "Your changes have been saved."}
            </Alert>
          ) : null}
          {saveError && saveState === "error" ? (
            <Alert tone="error" title={saveError.kind === "validation" ? "Check the form" : "Couldn't save"}>
              {saveError.message}
            </Alert>
          ) : null}
          {actionError ? (
            <Alert tone="error" title="That action didn't go through">
              {actionError}
            </Alert>
          ) : null}

          <Card className="space-y-3 p-5">
            <h2 className="text-h4 text-foreground">Review</h2>
            <div className="flex flex-wrap gap-3">
              {canSubmit ? (
                <Button onClick={onSubmit} loading={actionBusy}>
                  Submit for review
                </Button>
              ) : null}
              {canWithdraw ? (
                <Button variant="outline" onClick={onWithdraw} loading={actionBusy}>
                  Withdraw
                </Button>
              ) : null}
            </div>
          </Card>

          <AidxImageUpload
            imageUrl={loaded.hasImage ? newsImageUrl(loaded.id, reloadKey) : null}
            currentAltText={loaded.imageAltText}
            disabled={!isEditable || saveState === "saving" || actionBusy}
            helpText={status === "Published" ? "This story is published, so a new image opens a pending revision." : "Shown on this story's public page once approved."}
            onUpload={async (file, altText) => {
              const token = await getToken();
              await uploadAuthoringNewsImage(token, id, file, altText);
              setReloadKey((key) => key + 1);
            }}
          />

          <form
            onSubmit={(event) => {
              event.preventDefault();
              void onSave();
            }}
            className="space-y-6"
          >
            <NewsForm
              value={value}
              onChange={(next) => {
                setValue(next);
                setDirty(true);
                setSaveState("dirty");
              }}
              fieldErrors={fieldErrors}
              disabled={!isEditable || saveState === "saving" || actionBusy}
              researchers={researchers}
            />
            <div className="flex justify-end">
              <Button type="submit" size="lg" loading={saveState === "saving"} loadingText="Saving…" disabled={!isEditable || actionBusy}>
                Save changes
              </Button>
            </div>
          </form>

          <Card className="space-y-3 p-5">
            <button type="button" className="flex w-full items-center justify-between text-left" onClick={() => setShowPreview((v) => !v)}>
              <h2 className="text-h4 text-foreground">Draft preview</h2>
              {showPreview ? <EyeOff aria-hidden="true" className="size-4" /> : <Eye aria-hidden="true" className="size-4" />}
            </button>
            {showPreview ? (
              <div className="space-y-4 border-t border-border pt-4">
                <h3 className="text-h3 text-foreground">{value.title}</h3>
                <p className="text-body text-foreground-secondary">{value.summary}</p>
                <AidxRichText text={value.body} className="space-y-4" />
              </div>
            ) : null}
          </Card>

          <Card className="space-y-3 p-5">
            <h2 className="text-h4 text-foreground">History</h2>
            <AidxRevisionHistory contentType="news" id={id} />
          </Card>
        </>
      ) : null}
    </div>
  );
}
