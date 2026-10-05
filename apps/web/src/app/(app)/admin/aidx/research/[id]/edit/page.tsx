"use client";

import * as React from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { SearchX } from "lucide-react";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { describeAdminError, type AdminOpportunityError } from "@/lib/aidx/admin";
import { getAdminResearchArea, updateAdminResearchArea, type AdminResearchArea, type ResearchAreaInput } from "@/lib/aidx/cms";
import { ADMIN_NAV } from "../../../../_nav";
import { ResearchAreaForm } from "../../_components/research-area-form";

function toInput(area: AdminResearchArea): ResearchAreaInput {
  return {
    name: area.name,
    slug: area.slug,
    description: area.description,
    sortOrder: area.sortOrder,
  };
}

export default function EditResearchAreaPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const { getToken } = useAuth();

  const [loaded, setLoaded] = React.useState<AdminResearchArea | null>(null);
  const [value, setValue] = React.useState<ResearchAreaInput | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<AdminOpportunityError | null>(null);
  const [saving, setSaving] = React.useState(false);
  const [saveError, setSaveError] = React.useState<AdminOpportunityError | null>(null);
  const [notice, setNotice] = React.useState<string | null>(null);
  const [reloadKey, setReloadKey] = React.useState(0);

  React.useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      setLoadError(null);
      try {
        const token = await getToken();
        const area = await getAdminResearchArea(token, id);
        if (cancelled) return;
        setLoaded(area);
        setValue(toInput(area));
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
      const updated = await updateAdminResearchArea(token, id, value);
      setNotice("Changes saved.");
      setLoaded((current) => (current ? { ...current, name: value.name, slug: updated.slug ?? current.slug } : current));
      setValue((current) => (current ? { ...current, slug: updated.slug ?? current.slug } : current));
    } catch (caught) {
      setSaveError(describeAdminError(caught));
    } finally {
      setSaving(false);
    }
  }

  const fieldErrors = saveError?.kind === "validation" ? saveError.fields : {};

  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="max-w-3xl space-y-6">
        <nav aria-label="Breadcrumb" className="text-small text-muted-foreground">
          <Link href="/admin/aidx/research" className="underline-offset-4 hover:underline">
            Research areas
          </Link>
          <span aria-hidden="true"> / </span>
          <span className="text-foreground">Edit</span>
        </nav>

        {loading ? (
          <div className="space-y-4" aria-busy="true" aria-label="Loading research area">
            <Skeleton className="h-8 w-2/3" />
            <Skeleton className="h-64 w-full" />
          </div>
        ) : null}

        {!loading && loadError?.kind === "not-found" ? (
          <EmptyState
            icon={<SearchX aria-hidden="true" className="size-6" />}
            title="Research area not found"
            description={loadError.message}
            action={
              <Button asChild variant="outline">
                <Link href="/admin/aidx/research">Back to research areas</Link>
              </Button>
            }
          />
        ) : null}

        {!loading && loadError && loadError.kind !== "not-found" ? (
          <Alert tone={loadError.kind === "unauthenticated" ? "warning" : "error"} title="Unable to load this research area">
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
              <h1 className="break-words text-h2 text-foreground">{loaded.name}</h1>
              <p className="break-all text-small text-muted-foreground">Public page: /aidx/research/{loaded.slug}</p>
            </div>

            {notice ? (
              <Alert tone="success" title="Done">
                {notice}
              </Alert>
            ) : null}

            {saveError ? (
              <Alert tone="error" title={saveError.kind === "validation" ? "Check the form" : "Couldn't save changes"}>
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
              <ResearchAreaForm value={value} onChange={setValue} fieldErrors={fieldErrors} disabled={saving} />
              <div className="flex justify-end">
                <Button type="submit" size="lg" loading={saving} loadingText="Saving…">
                  Save changes
                </Button>
              </div>
            </form>
          </>
        ) : null}
      </div>
    </DashboardShell>
  );
}
