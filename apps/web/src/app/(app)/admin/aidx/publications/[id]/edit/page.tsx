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
import { AidxPdfUpload } from "@/components/aidx/aidx-pdf-upload";
import { AidxWorkflowStatusBadge } from "@/components/aidx/aidx-workflow-status-badge";
import { describeAdminError, listPublishedProjectOptions, type AdminOpportunityError } from "@/lib/aidx/admin";
import { listResearchAreaOptions } from "@/lib/aidx/cms";
import { labels } from "@/lib/aidx/format";
import { listAdminPeople, type AdminPersonSummary } from "@/lib/aidx/people-cms";
import {
  archiveAdminPublication,
  deleteAdminPublication,
  getAdminPublication,
  publicationPdfUrl,
  publishAdminPublication,
  updateAdminPublication,
  uploadAdminPublicationPdf,
  type AdminPublicationDetail,
} from "@/lib/aidx/publications-cms";
import type { ResearchArea } from "@/lib/aidx/types";
import { ADMIN_NAV } from "../../../../_nav";
import { ConfirmDialog } from "../../../opportunities/_components/confirm-dialog";
import { PublicationForm, type PublicationFormValue, type ProjectOption } from "../../_components/publication-form";
import { fromDetail, projectOptions, toInput } from "../../_components/mapping";

type Pending = "publish" | "archive" | null;

export default function EditPublicationPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const router = useRouter();
  const { getToken } = useAuth();

  const [loaded, setLoaded] = React.useState<AdminPublicationDetail | null>(null);
  const [value, setValue] = React.useState<PublicationFormValue | null>(null);
  const [areas, setAreas] = React.useState<ResearchArea[]>([]);
  const [researchers, setResearchers] = React.useState<AdminPersonSummary[]>([]);
  const [projects, setProjects] = React.useState<ProjectOption[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<AdminOpportunityError | null>(null);
  const [saving, setSaving] = React.useState(false);
  const [saveError, setSaveError] = React.useState<AdminOpportunityError | null>(null);
  const [notice, setNotice] = React.useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = React.useState(false);
  const [deleting, setDeleting] = React.useState(false);
  const [reloadKey, setReloadKey] = React.useState(0);
  const [pending, setPending] = React.useState<Pending>(null);
  const [actionBusy, setActionBusy] = React.useState(false);

  React.useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      setLoadError(null);
      try {
        const token = await getToken();
        const [publication, areaList, people, publishedProjects] = await Promise.all([
          getAdminPublication(token, id),
          listResearchAreaOptions().catch(() => [] as ResearchArea[]),
          listAdminPeople(token, { pageSize: 100 }).then((p) => p.items).catch(() => [] as AdminPersonSummary[]),
          listPublishedProjectOptions().then((p) => p.items).catch(() => []),
        ]);
        if (cancelled) return;
        setLoaded(publication);
        setValue(fromDetail(publication));
        setAreas(areaList);
        setResearchers(people);
        setProjects(projectOptions(publishedProjects, publication.projects));
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
      const result = await updateAdminPublication(token, id, toInput(value, loaded?.version ?? null));
      setNotice(
        result.revisionOfId
          ? "Saved as a pending revision. The published publication is unchanged until this revision is reviewed and approved."
          : "Changes saved."
      );
      setReloadKey((key) => key + 1);
    } catch (caught) {
      setSaveError(describeAdminError(caught));
    } finally {
      setSaving(false);
    }
  }

  async function runLifecycle(kind: Exclude<Pending, null>) {
    setActionBusy(true);
    setSaveError(null);
    setNotice(null);
    try {
      const token = await getToken();
      if (kind === "publish") {
        await publishAdminPublication(token, id, loaded?.version ?? null);
        setNotice("Published. It is now visible on the AIDX publications page.");
      } else {
        await archiveAdminPublication(token, id, loaded?.version ?? null);
        setNotice("Archived. It is no longer public.");
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

  async function runDelete() {
    setDeleting(true);
    try {
      const token = await getToken();
      await deleteAdminPublication(token, id);
      router.push("/admin/aidx/publications");
    } catch (caught) {
      setSaveError(describeAdminError(caught));
      setConfirmDelete(false);
      setDeleting(false);
    }
  }

  const fieldErrors = saveError?.kind === "validation" ? saveError.fields : {};

  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="max-w-3xl space-y-6">
        <nav aria-label="Breadcrumb" className="text-small text-muted-foreground">
          <Link href="/admin/aidx/publications" className="underline-offset-4 hover:underline">
            Publications
          </Link>
          <span aria-hidden="true"> / </span>
          <span className="text-foreground">Edit</span>
        </nav>

        {loading ? (
          <div className="space-y-4" aria-busy="true" aria-label="Loading publication">
            <Skeleton className="h-8 w-2/3" />
            <Skeleton className="h-64 w-full" />
          </div>
        ) : null}

        {!loading && loadError?.kind === "not-found" ? (
          <EmptyState
            icon={<SearchX aria-hidden="true" className="size-6" />}
            title="Publication not found"
            description={loadError.message}
            action={
              <Button asChild variant="outline">
                <Link href="/admin/aidx/publications">Back to publications</Link>
              </Button>
            }
          />
        ) : null}

        {!loading && loadError && loadError.kind !== "not-found" ? (
          <Alert tone={loadError.kind === "unauthenticated" ? "warning" : "error"} title="Unable to load this publication">
            <p>{loadError.message}</p>
            {loadError.kind !== "forbidden" ? (
              <Button variant="outline" className="mt-3" onClick={() => setReloadKey((key) => key + 1)}>
                Try again
              </Button>
            ) : null}
          </Alert>
        ) : null}

        {!loading && loaded && value ? (
          <>
            <div className="space-y-1">
              <h1 className="break-words text-h2 text-foreground">{loaded.title}</h1>
              <div className="flex flex-wrap items-center gap-3 text-small text-muted-foreground">
                <AidxWorkflowStatusBadge status={loaded.status} />
                <span>{labels.publication(loaded.publicationType)}</span>
                <span>{loaded.year}</span>
              </div>
            </div>

            {notice ? (
              <Alert tone="success" title="Done">
                {notice}
              </Alert>
            ) : null}

            {saveError?.kind === "conflict" ? (
              <Alert tone="warning" title="This was edited elsewhere">
                <p>
                  Someone else changed this publication after you loaded it, so your save was refused rather than
                  silently overwriting their edit. Nothing has been saved and the form below still shows exactly
                  what you typed.
                </p>
                <p className="mt-2 text-small text-muted-foreground">
                  Reloading replaces the form with the current server content, discarding what you typed here. Copy
                  anything you want to keep first.
                </p>
                <Button variant="outline" className="mt-3" onClick={() => setReloadKey((key) => key + 1)}>
                  Discard my edits and reload the latest version
                </Button>
              </Alert>
            ) : saveError ? (
              <Alert tone="error" title={saveError.kind === "validation" ? "Check the form" : "Couldn't complete that change"}>
                {saveError.message}
              </Alert>
            ) : null}

            <Card className="space-y-4 p-5">
              <h2 className="text-h4 text-foreground">Publishing</h2>
              <p className="text-small text-muted-foreground">
                {loaded.status === "Published"
                  ? "This publication is public. Archiving it removes it from the public publications page."
                  : loaded.status === "Draft" || loaded.status === "ChangesRequested"
                    ? "Drafts are private. Publish when the publication is ready to show."
                    : loaded.status === "Archived"
                      ? "This publication is archived and private. Publish it again to make it public."
                      : "This publication is submitted for review. It will become public once approved."}
              </p>
              <div className="flex flex-wrap gap-3">
                {loaded.status !== "Published" ? (
                  <Button onClick={() => setPending("publish")} disabled={actionBusy || saving}>
                    Publish
                  </Button>
                ) : (
                  <Button variant="outline" onClick={() => setPending("archive")} disabled={actionBusy || saving}>
                    Archive
                  </Button>
                )}
              </div>
            </Card>

            <AidxPdfUpload
              pdfUrl={loaded.hasPdf ? publicationPdfUrl(loaded.id) : null}
              disabled={saving || deleting || actionBusy}
              helpText={
                loaded.status === "Published"
                  ? "This publication is published, so a new PDF opens a pending revision — it replaces the live file only once that revision is approved."
                  : "The paper's PDF, offered as a download on its public page."
              }
              onUpload={async (file) => {
                const token = await getToken();
                await uploadAdminPublicationPdf(token, id, file);
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
              <PublicationForm
                value={value}
                onChange={setValue}
                fieldErrors={fieldErrors}
                disabled={saving || deleting || actionBusy}
                areas={areas}
                researchers={researchers}
                projects={projects}
              />
              <div className="flex flex-wrap items-center justify-between gap-3">
                <Button type="button" variant="outline" onClick={() => setConfirmDelete(true)} disabled={saving || deleting || actionBusy}>
                  Delete publication
                </Button>
                <Button type="submit" size="lg" loading={saving} loadingText="Saving…" disabled={deleting || actionBusy}>
                  Save changes
                </Button>
              </div>
            </form>

            <Card className="p-5">
              <p className="text-caption text-muted-foreground">
                Deleting removes this publication and its author, area and project links. The researchers, research
                areas and projects themselves are kept.
              </p>
            </Card>
          </>
        ) : null}
      </div>

      <ConfirmDialog
        open={confirmDelete}
        onOpenChange={(open) => !open && setConfirmDelete(false)}
        title="Delete publication?"
        description={`"${loaded?.title ?? ""}" will be permanently removed, with its author and link records. The researchers, areas and projects it refers to are kept. This cannot be undone.`}
        confirmLabel="Delete publication"
        destructive
        busy={deleting}
        onConfirm={() => void runDelete()}
      />
      <ConfirmDialog
        open={pending === "publish"}
        onOpenChange={(open) => !open && setPending(null)}
        title="Publish publication?"
        description="This publication will become visible on the AIDX Lab publications page."
        confirmLabel="Publish"
        busy={actionBusy}
        onConfirm={() => void runLifecycle("publish")}
      />
      <ConfirmDialog
        open={pending === "archive"}
        onOpenChange={(open) => !open && setPending(null)}
        title="Archive publication?"
        description="It will no longer appear in public publication listings. You can publish it again later."
        confirmLabel="Archive"
        busy={actionBusy}
        onConfirm={() => void runLifecycle("archive")}
      />
    </DashboardShell>
  );
}
