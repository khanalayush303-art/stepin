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
import {
  deleteAdminOpportunity,
  describeAdminError,
  getAdminOpportunity,
  listPublishedProjectOptions,
  publishAdminOpportunity,
  unpublishAdminOpportunity,
  updateAdminOpportunity,
  type AdminOpportunityError,
  type AidxOpportunityAdmin,
  type AidxOpportunityInput,
} from "@/lib/aidx/admin";
import type { ProjectSummary } from "@/lib/aidx/types";
import { formatDate } from "@/lib/utils";
import { ADMIN_NAV } from "../../../../_nav";
import { ConfirmDialog } from "../../_components/confirm-dialog";
import { OpportunityForm } from "../../_components/opportunity-form";
import { OpportunityStatusBadge } from "../../_components/opportunity-status-badge";

type Pending = "publish" | "unpublish" | "delete" | null;

function toInput(item: AidxOpportunityAdmin): AidxOpportunityInput {
  return {
    title: item.title,
    description: item.description,
    employmentType: item.employmentType,
    workplaceType: item.workplaceType,
    location: item.location,
    compensation: item.compensation,
    skills: item.skills,
    aidxProjectId: item.aidxProjectId,
  };
}

export default function EditAidxOpportunityPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const router = useRouter();
  const { getToken } = useAuth();

  const [item, setItem] = React.useState<AidxOpportunityAdmin | null>(null);
  const [value, setValue] = React.useState<AidxOpportunityInput | null>(null);
  const [projects, setProjects] = React.useState<ProjectSummary[]>([]);
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
        const [opportunity, page] = await Promise.all([
          getAdminOpportunity(token, id),
          listPublishedProjectOptions().catch(() => ({ items: [] as ProjectSummary[] })),
        ]);
        if (cancelled) return;
        setItem(opportunity);
        setValue(toInput(opportunity));
        setProjects(page.items);
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
      const updated = await updateAdminOpportunity(token, id, value);
      setItem(updated);
      setValue(toInput(updated));
      setNotice("Changes saved.");
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
        await deleteAdminOpportunity(token, id);
        router.push("/admin/aidx/opportunities");
        return;
      }
      const updated =
        kind === "publish"
          ? await publishAdminOpportunity(token, id)
          : await unpublishAdminOpportunity(token, id);
      setItem(updated);
      setValue(toInput(updated));
      setNotice(kind === "publish" ? "Published. It is now listed on the AIDX Lab opportunities page." : "Unpublished. It is no longer listed publicly.");
      setPending(null);
    } catch (caught) {
      setSaveError(describeAdminError(caught));
      setPending(null);
    } finally {
      setActionBusy(false);
    }
  }

  const status = item?.status;
  const canPublish = status === "Draft" || status === "Unpublished";
  const canUnpublish = status === "Published";
  const canDelete = status === "Draft";
  const fieldErrors = saveError?.kind === "validation" ? saveError.fields : {};

  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="max-w-3xl space-y-6">
        <nav aria-label="Breadcrumb" className="text-small text-muted-foreground">
          <Link href="/admin/aidx/opportunities" className="underline-offset-4 hover:underline">
            Research opportunities
          </Link>
          <span aria-hidden="true"> / </span>
          <span className="text-foreground">Edit</span>
        </nav>

        {loading ? <EditSkeleton /> : null}

        {!loading && loadError?.kind === "not-found" ? (
          <EmptyState
            icon={<SearchX aria-hidden="true" className="size-6" />}
            title="Research opportunity not found"
            description={loadError.message}
            action={
              <Button asChild variant="outline">
                <Link href="/admin/aidx/opportunities">Back to research opportunities</Link>
              </Button>
            }
          />
        ) : null}

        {!loading && loadError && loadError.kind !== "not-found" ? (
          <Alert tone={loadError.kind === "unauthenticated" ? "warning" : "error"} title="Unable to load this opportunity">
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
                <OpportunityStatusBadge status={item.status} />
                {item.publishedAt ? <span>Published {formatDate(item.publishedAt)}</span> : null}
                {item.updatedAt ? <span>Last updated {formatDate(item.updatedAt)}</span> : null}
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
              <h2 className="text-h4 text-foreground">Lifecycle</h2>
              <p className="text-small text-muted-foreground">
                {status === "Published"
                  ? "This opportunity is live. Unpublishing removes it from the public list but keeps the record and its project link."
                  : status === "Draft"
                    ? "Drafts are not public. Publish when the details are ready, or delete the draft if it is no longer needed."
                    : "This opportunity is unpublished. Publish it again to list it publicly."}
              </p>
              <div className="flex flex-wrap gap-3">
                {canPublish ? (
                  <Button onClick={() => setPending("publish")} disabled={actionBusy || saving}>
                    Publish
                  </Button>
                ) : null}
                {canUnpublish ? (
                  <Button variant="outline" onClick={() => setPending("unpublish")} disabled={actionBusy || saving}>
                    Unpublish
                  </Button>
                ) : null}
                {canDelete ? (
                  <Button variant="outline" onClick={() => setPending("delete")} disabled={actionBusy || saving}>
                    Delete draft
                  </Button>
                ) : null}
              </div>
            </Card>

            <form
              onSubmit={(event) => {
                event.preventDefault();
                void onSave();
              }}
              className="space-y-6"
            >
              <OpportunityForm
                value={value}
                onChange={setValue}
                fieldErrors={fieldErrors}
                disabled={saving || actionBusy}
                projects={projects}
                currentProject={item.aidxProjectId && item.aidxProjectTitle ? { id: item.aidxProjectId, title: item.aidxProjectTitle } : null}
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
        title="Publish research opportunity?"
        description="This opportunity will be listed on the AIDX Lab opportunities page. Candidates can then open it and apply."
        confirmLabel="Publish"
        busy={actionBusy}
        onConfirm={() => void runAction("publish")}
      />
      <ConfirmDialog
        open={pending === "unpublish"}
        onOpenChange={(open) => !open && setPending(null)}
        title="Unpublish research opportunity?"
        description="It will be removed from the public opportunities list. The record and its project link are kept, and you can publish it again later."
        confirmLabel="Unpublish"
        busy={actionBusy}
        onConfirm={() => void runAction("unpublish")}
      />
      <ConfirmDialog
        open={pending === "delete"}
        onOpenChange={(open) => !open && setPending(null)}
        title="Delete draft?"
        description="This permanently removes the draft. This cannot be undone."
        confirmLabel="Delete draft"
        destructive
        busy={actionBusy}
        onConfirm={() => void runAction("delete")}
      />
    </DashboardShell>
  );
}

function EditSkeleton() {
  return (
    <div className="space-y-4" aria-busy="true" aria-label="Loading research opportunity">
      <Skeleton className="h-8 w-2/3" />
      <Skeleton className="h-4 w-1/3" />
      <Skeleton className="h-40 w-full" />
      <Skeleton className="h-64 w-full" />
    </div>
  );
}
