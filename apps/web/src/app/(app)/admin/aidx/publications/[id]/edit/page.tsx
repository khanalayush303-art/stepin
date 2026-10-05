"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { SearchX } from "lucide-react";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { describeAdminError, listPublishedProjectOptions, type AdminOpportunityError } from "@/lib/aidx/admin";
import { listResearchAreaOptions } from "@/lib/aidx/cms";
import { labels } from "@/lib/aidx/format";
import { listAdminPeople, type AdminPersonSummary } from "@/lib/aidx/people-cms";
import {
  deleteAdminPublication,
  getAdminPublication,
  updateAdminPublication,
  type AdminPublicationDetail,
} from "@/lib/aidx/publications-cms";
import type { ResearchArea } from "@/lib/aidx/types";
import { ADMIN_NAV } from "../../../../_nav";
import { ConfirmDialog } from "../../../opportunities/_components/confirm-dialog";
import { PublicationForm, type PublicationFormValue, type ProjectOption } from "../../_components/publication-form";
import { fromDetail, projectOptions, toInput } from "../../_components/mapping";

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
      await updateAdminPublication(token, id, toInput(value));
      setNotice("Changes saved.");
      setReloadKey((key) => key + 1);
    } catch (caught) {
      setSaveError(describeAdminError(caught));
    } finally {
      setSaving(false);
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
                <Badge tone={loaded.published ? "success" : "neutral"} variant="subtle" showDot>
                  {loaded.published ? "Public" : "Hidden"}
                </Badge>
                <span>{labels.publication(loaded.publicationType)}</span>
                <span>{loaded.year}</span>
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
                disabled={saving || deleting}
                areas={areas}
                researchers={researchers}
                projects={projects}
              />
              <div className="flex flex-wrap items-center justify-between gap-3">
                <Button type="button" variant="outline" onClick={() => setConfirmDelete(true)} disabled={saving || deleting}>
                  Delete publication
                </Button>
                <Button type="submit" size="lg" loading={saving} loadingText="Saving…" disabled={deleting}>
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
    </DashboardShell>
  );
}
