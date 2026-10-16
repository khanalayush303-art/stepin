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
import type { AidxResearcherPickerOption } from "@/components/aidx/aidx-researcher-picker";
import { ProjectForm } from "@/app/(app)/admin/aidx/projects/_components/project-form";
import { describeAdminError, type AdminOpportunityError } from "@/lib/aidx/admin";
import {
  getAuthoringProject,
  updateAuthoringProject,
  uploadAuthoringProjectImage,
} from "@/lib/aidx/authoring";
import { listResearchAreaOptions, listTechnologyOptions, projectImageUrl, type AdminProjectDetail, type ProjectInput } from "@/lib/aidx/cms";
import { listAuditEntries, submitForReview, withdraw, type AidxAuditEntry } from "@/lib/aidx/workflow";
import { listAdminPeople } from "@/lib/aidx/people-cms";
import type { ResearchArea } from "@/lib/aidx/types";
import { useUnsavedChangesGuard } from "@/lib/aidx/use-unsaved-changes-guard";

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
    researchers: project.researchers.map((r) => ({ researcherId: r.researcherId, role: r.role })),
    outcomes: project.outcomes,
    milestones: project.milestones,
    expectedVersion: project.version,
  };
}

export default function EditWorkspaceProjectPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const { getToken } = useAuth();

  const [loaded, setLoaded] = React.useState<AdminProjectDetail | null>(null);
  const [value, setValue] = React.useState<ProjectInput | null>(null);
  const [dirty, setDirty] = React.useState(false);
  const [areas, setAreas] = React.useState<ResearchArea[]>([]);
  const [researchers, setResearchers] = React.useState<AidxResearcherPickerOption[]>([]);
  const [technologySuggestions, setTechnologySuggestions] = React.useState<string[]>([]);
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
        const [project, options, people, technologies, audit] = await Promise.all([
          getAuthoringProject(token, id),
          listResearchAreaOptions().catch(() => [] as ResearchArea[]),
          listAdminPeople(token, { pageSize: 200 }).then((p) => p.items).catch(() => []),
          listTechnologyOptions().catch(() => [] as string[]),
          listAuditEntries(token, "project", id).catch(() => [] as AidxAuditEntry[]),
        ]);
        if (cancelled) return;
        setLoaded(project);
        setValue(toInput(project));
        setDirty(false);
        setAreas(options);
        setResearchers(people.map((p) => ({ id: p.id, displayName: p.displayName })));
        setTechnologySuggestions(technologies);
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
      await updateAuthoringProject(token, id, value);
      setSaveState("saved");
      setDirty(false);
      setReloadKey((key) => key + 1);
    } catch (caught) {
      const described = describeAdminError(caught);
      setSaveError(described);
      setSaveState("error");
    }
  }

  async function onSubmit() {
    if (!loaded) return;
    setActionBusy(true);
    setActionError(null);
    try {
      const token = await getToken();
      await submitForReview(token, "project", id, loaded.version ?? "");
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
      await withdraw(token, "project", id, loaded.version ?? "");
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
    <div className="container-page max-w-4xl space-y-6 py-10">
      <nav aria-label="Breadcrumb" className="text-small text-muted-foreground">
        <Link href="/aidx/workspace" className="underline-offset-4 hover:underline">
          My AIDX workspace
        </Link>
        <span aria-hidden="true"> / </span>
        <span className="text-foreground">Edit project</span>
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
              <Link href="/aidx/workspace">Back to my workspace</Link>
            </Button>
          }
        />
      ) : null}

      {!loading && loadError && loadError.kind !== "not-found" ? (
        <Alert tone={loadError.kind === "unauthenticated" ? "warning" : "error"} title="Unable to load this project">
          {loadError.message}
        </Alert>
      ) : null}

      {!loading && loaded && value ? (
        <>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="space-y-1">
              <h1 className="break-words text-h2 text-foreground">{loaded.title}</h1>
              <div className="flex flex-wrap items-center gap-3 text-small text-muted-foreground">
                <AidxWorkflowStatusBadge status={loaded.status} />
                {loaded.revisionOfId ? <span>· editing a pending revision</span> : null}
              </div>
            </div>
            <AidxSaveStatus state={saveState} errorMessage={saveError?.message} />
          </div>

          {!isEditable ? (
            <Alert tone="warning" title="This is read-only right now">
              This project is {loaded.status === "Submitted" ? "submitted" : "under review"}. Withdraw it first if you
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
                ? "Saved as a pending revision. The published project is unchanged until this revision is reviewed and approved."
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
            <p className="text-small text-muted-foreground">
              {status === "Published"
                ? "This project is published. Saving a change here opens a pending revision for review — the live page is unaffected until it's approved."
                : canSubmit
                  ? "Submit when the project is ready for an editor to review."
                  : "Withdraw to make more changes before resubmitting."}
            </p>
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
            imageUrl={loaded.hasImage ? projectImageUrl(loaded.id, reloadKey) : null}
            currentAltText={loaded.imageAltText}
            disabled={!isEditable || saveState === "saving" || actionBusy}
            helpText={
              status === "Published"
                ? "This project is published, so a new image opens a pending revision."
                : "Shown on this project's public page once approved."
            }
            onUpload={async (file, altText) => {
              const token = await getToken();
              await uploadAuthoringProjectImage(token, id, file, altText);
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
              onChange={(next) => {
                setValue(next);
                setDirty(true);
                setSaveState("dirty");
              }}
              fieldErrors={fieldErrors}
              disabled={!isEditable || saveState === "saving" || actionBusy}
              areas={areas}
              researchers={researchers}
              technologySuggestions={technologySuggestions}
            />
            <div className="flex justify-end">
              <Button type="submit" size="lg" loading={saveState === "saving"} loadingText="Saving…" disabled={!isEditable || actionBusy}>
                Save changes
              </Button>
            </div>
          </form>

          <Card className="space-y-3 p-5">
            <button
              type="button"
              className="flex w-full items-center justify-between text-left"
              onClick={() => setShowPreview((v) => !v)}
            >
              <h2 className="text-h4 text-foreground">Draft preview</h2>
              {showPreview ? <EyeOff aria-hidden="true" className="size-4" /> : <Eye aria-hidden="true" className="size-4" />}
            </button>
            {showPreview ? (
              <div className="space-y-4 border-t border-border pt-4">
                <h3 className="text-h3 text-foreground">{value.title}</h3>
                <p className="text-body text-foreground-secondary">{value.shortDescription}</p>
                <AidxRichText text={value.description} className="space-y-4" />
                {value.outcomes ? (
                  <>
                    <h4 className="text-h4 text-foreground">Outcomes</h4>
                    <AidxRichText text={value.outcomes} className="space-y-4" />
                  </>
                ) : null}
                {value.milestones.length > 0 ? (
                  <>
                    <h4 className="text-h4 text-foreground">Milestones</h4>
                    <ul className="space-y-2">
                      {value.milestones.map((m, i) => (
                        <li key={i} className="rounded-md border border-border p-2 text-small">
                          <p className="font-medium text-foreground">{m.title}</p>
                          {m.description ? <AidxRichText text={m.description} proseClassName="text-small text-muted-foreground" /> : null}
                        </li>
                      ))}
                    </ul>
                  </>
                ) : null}
                <p className="text-caption text-muted-foreground">
                  This preview is rendered from your unsaved draft using the same components as the public page — it
                  is not visible to anyone else until approved.
                </p>
              </div>
            ) : null}
          </Card>

          <Card className="space-y-3 p-5">
            <h2 className="text-h4 text-foreground">History</h2>
            <AidxRevisionHistory contentType="project" id={id} />
          </Card>
        </>
      ) : null}
    </div>
  );
}
