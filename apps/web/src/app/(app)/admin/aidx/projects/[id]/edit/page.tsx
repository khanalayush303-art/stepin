"use client";

import * as React from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { SearchX } from "lucide-react";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { AidxImageUpload } from "@/components/aidx/aidx-image-upload";
import type { AidxResearcherPickerOption } from "@/components/aidx/aidx-researcher-picker";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { describeAdminError, type AdminOpportunityError } from "@/lib/aidx/admin";
import {
  archiveAdminProject,
  getAdminProject,
  listResearchAreaOptions,
  listTechnologyOptions,
  projectImageUrl,
  publishAdminProject,
  updateAdminProject,
  uploadAdminProjectImage,
  type AdminProjectDetail,
  type ProjectInput,
} from "@/lib/aidx/cms";
import { listAdminPeople } from "@/lib/aidx/people-cms";
import type { ResearchArea } from "@/lib/aidx/types";
import { formatDate } from "@/lib/utils";
import { ADMIN_NAV } from "../../../../_nav";
import { ConfirmDialog } from "../../../opportunities/_components/confirm-dialog";
import { ProjectForm } from "../../_components/project-form";
import { ProjectStatusBadge } from "../../_components/project-status-badge";

type Pending = "publish" | "archive" | null;

function toInput(project: AdminProjectDetail): ProjectInput {
  return {
    title: project.title,
    slug: project.slug,
    shortDescription: project.shortDescription,
    description: project.description,
    startDate: project.startDate,
    endDate: project.endDate,
    externalUrl: project.externalUrl,
    featured: project.featured,
    researchAreaIds: project.researchAreaIds,
    technologies: project.technologies,
    // Carried through so a save never drops team links that this form does not edit yet.
    researchers: project.researchers.map((r) => ({ researcherId: r.researcherId, role: r.role })),
    outcomes: project.outcomes,
    milestones: project.milestones,
    expectedVersion: project.version,
  };
}

export default function EditAidxProjectPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const { getToken } = useAuth();

  const [loaded, setLoaded] = React.useState<AdminProjectDetail | null>(null);
  const [value, setValue] = React.useState<ProjectInput | null>(null);
  const [areas, setAreas] = React.useState<ResearchArea[]>([]);
  const [researchers, setResearchers] = React.useState<AidxResearcherPickerOption[]>([]);
  const [technologySuggestions, setTechnologySuggestions] = React.useState<string[]>([]);
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
        const [project, options, people, technologies] = await Promise.all([
          getAdminProject(token, id),
          listResearchAreaOptions().catch(() => [] as ResearchArea[]),
          listAdminPeople(token, { pageSize: 200 }).then((p) => p.items).catch(() => []),
          listTechnologyOptions().catch(() => [] as string[]),
        ]);
        if (cancelled) return;
        setLoaded(project);
        setValue(toInput(project));
        setAreas(options);
        setResearchers(people.map((p) => ({ id: p.id, displayName: p.displayName })));
        setTechnologySuggestions(technologies);
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
      const result = await updateAdminProject(token, id, value);
      setNotice(
        result.revisionOfId
          ? "Saved as a pending revision. The published project is unchanged until this revision is reviewed and approved."
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
        await publishAdminProject(token, id, loaded?.version ?? null);
        setNotice("Published. It is now visible on the AIDX projects page.");
      } else {
        await archiveAdminProject(token, id, loaded?.version ?? null);
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

  const fieldErrors = saveError?.kind === "validation" ? saveError.fields : {};

  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="max-w-3xl space-y-6">
        <nav aria-label="Breadcrumb" className="text-small text-muted-foreground">
          <Link href="/admin/aidx/projects" className="underline-offset-4 hover:underline">
            Research projects
          </Link>
          <span aria-hidden="true"> / </span>
          <span className="text-foreground">Edit</span>
        </nav>

        {loading ? (
          <div className="space-y-4" aria-busy="true" aria-label="Loading project">
            <Skeleton className="h-8 w-2/3" />
            <Skeleton className="h-64 w-full" />
          </div>
        ) : null}

        {!loading && loadError?.kind === "not-found" ? (
          <EmptyState
            icon={<SearchX aria-hidden="true" className="size-6" />}
            title="Project not found"
            description={loadError.message}
            action={
              <Button asChild variant="outline">
                <Link href="/admin/aidx/projects">Back to projects</Link>
              </Button>
            }
          />
        ) : null}

        {!loading && loadError && loadError.kind !== "not-found" ? (
          <Alert tone={loadError.kind === "unauthenticated" ? "warning" : "error"} title="Unable to load this project">
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
                <ProjectStatusBadge status={loaded.status} />
                {loaded.publishedAt ? <span>Published {formatDate(loaded.publishedAt)}</span> : null}
                <span className="break-all">Public page: /aidx/projects/{loaded.slug}</span>
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
                  Someone else changed this project after you loaded it, so your save was refused rather than
                  silently overwriting their edit. Nothing has been saved and the form below still shows exactly
                  what you typed — it is safe to copy anything you want to keep before deciding what to do next.
                </p>
                <p className="mt-2 text-small text-muted-foreground">
                  Reloading replaces the form with the current server content, discarding what you typed here. If
                  you want to keep your own edits, copy them somewhere first, then reload, then re-apply them on
                  top of the latest version before saving again.
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
                  ? "This project is public. Archiving it removes it from the public projects page."
                  : loaded.status === "Draft"
                    ? "Drafts are private. Publish when the project is ready to show."
                    : "This project is archived and private. Publish it again to make it public."}
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

            <AidxImageUpload
              imageUrl={loaded.hasImage ? projectImageUrl(loaded.id, reloadKey) : null}
              currentAltText={loaded.imageAltText}
              disabled={saving || actionBusy}
              helpText={
                loaded.status === "Published"
                  ? "This project is published, so a new image opens a pending revision — it replaces the live image only once that revision is approved."
                  : "Shown on this project's public page."
              }
              onUpload={async (file, altText) => {
                const token = await getToken();
                await uploadAdminProjectImage(token, id, file, altText);
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
              <ProjectForm
                value={value}
                onChange={setValue}
                fieldErrors={fieldErrors}
                disabled={saving || actionBusy}
                areas={areas}
                researchers={researchers}
                technologySuggestions={technologySuggestions}
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
        title="Publish project?"
        description="This project will become visible on the AIDX Lab projects page."
        confirmLabel="Publish"
        busy={actionBusy}
        onConfirm={() => void runLifecycle("publish")}
      />
      <ConfirmDialog
        open={pending === "archive"}
        onOpenChange={(open) => !open && setPending(null)}
        title="Archive project?"
        description="It will no longer appear in public project listings. Linked research opportunities stay as they are, and you can publish the project again later."
        confirmLabel="Archive"
        busy={actionBusy}
        onConfirm={() => void runLifecycle("archive")}
      />
    </DashboardShell>
  );
}
