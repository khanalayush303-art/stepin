"use client";

import * as React from "react";
import Link from "next/link";
import { BookOpen, Pencil, Plus, Trash2 } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { describeAdminError, type AdminOpportunityError } from "@/lib/aidx/admin";
import { deleteAdminResearchArea, listResearchAreaOptions } from "@/lib/aidx/cms";
import type { ResearchArea } from "@/lib/aidx/types";
import { ADMIN_NAV } from "../../_nav";
import { ConfirmDialog } from "../opportunities/_components/confirm-dialog";

export default function AdminAidxResearchPage() {
  const { getToken } = useAuth();
  const [search, setSearch] = React.useState("");
  const [appliedSearch, setAppliedSearch] = React.useState("");
  const [areas, setAreas] = React.useState<ResearchArea[] | null>(null);
  const [error, setError] = React.useState<AdminOpportunityError | null>(null);
  const [notice, setNotice] = React.useState<string | null>(null);
  const [reloadKey, setReloadKey] = React.useState(0);
  const [toDelete, setToDelete] = React.useState<ResearchArea | null>(null);
  const [busy, setBusy] = React.useState(false);

  React.useEffect(() => {
    let cancelled = false;
    listResearchAreaOptions(appliedSearch)
      .then((items) => {
        if (!cancelled) {
          setAreas(items);
          setError(null);
        }
      })
      .catch((caught) => {
        if (!cancelled) setError(describeAdminError(caught));
      });
    return () => {
      cancelled = true;
    };
  }, [appliedSearch, reloadKey]);

  async function confirmDelete() {
    if (!toDelete) return;
    setBusy(true);
    try {
      const token = await getToken();
      await deleteAdminResearchArea(token, toDelete.id);
      setNotice(`"${toDelete.name}" was deleted.`);
      setToDelete(null);
      setReloadKey((key) => key + 1);
    } catch (caught) {
      setError(describeAdminError(caught));
      setToDelete(null);
    } finally {
      setBusy(false);
    }
  }

  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="space-y-8">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div className="space-y-1">
            <p className="text-caption font-semibold uppercase tracking-wide text-primary">AIDX Lab</p>
            <h1 className="text-h2 text-foreground">Research areas</h1>
            <p className="max-w-2xl text-body text-muted-foreground">
              The fields of research the lab works in. Research areas appear on the public research page as soon as they
              exist, and they have no draft stage.
            </p>
          </div>
          <Button asChild>
            <Link href="/admin/aidx/research/new">
              <Plus aria-hidden="true" className="size-4" />
              Create research area
            </Link>
          </Button>
        </div>

        <Card className="p-5">
          <form
            role="search"
            aria-label="Search research areas"
            className="flex flex-col gap-4 sm:flex-row sm:items-end"
            onSubmit={(event) => {
              event.preventDefault();
              setAppliedSearch(search.trim());
            }}
          >
            <div className="flex-1">
              <FormField id="research-search" label="Search by name">
                {(props) => (
                  <Input {...props} type="search" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search research areas" />
                )}
              </FormField>
            </div>
            <Button type="submit" variant="outline">
              Search
            </Button>
          </form>
        </Card>

        {notice ? (
          <Alert tone="success" title="Done">
            {notice}
          </Alert>
        ) : null}

        {error ? (
          <Alert tone="error" title="Unable to load research areas">
            <p>{error.message}</p>
            <Button variant="outline" className="mt-3" onClick={() => setReloadKey((key) => key + 1)}>
              Try again
            </Button>
          </Alert>
        ) : null}

        {!error && areas === null ? <ListSkeleton /> : null}

        {!error && areas !== null && areas.length === 0 ? (
          appliedSearch ? (
            <EmptyState
              icon={<BookOpen aria-hidden="true" className="size-6" />}
              title="No research areas match your search"
              description="Try a different name, or clear the search."
              action={
                <Button
                  variant="outline"
                  onClick={() => {
                    setSearch("");
                    setAppliedSearch("");
                  }}
                >
                  Clear search
                </Button>
              }
            />
          ) : (
            <EmptyState
              icon={<BookOpen aria-hidden="true" className="size-6" />}
              title="No research areas yet"
              description="Create the first AIDX Lab research area."
              action={
                <Button asChild>
                  <Link href="/admin/aidx/research/new">Create research area</Link>
                </Button>
              }
            />
          )
        ) : null}

        {!error && areas !== null && areas.length > 0 ? (
          <section aria-labelledby="areas-heading" className="space-y-4">
            <h2 id="areas-heading" className="text-h4 text-foreground">
              Research areas ({areas.length})
            </h2>
            <ul className="space-y-3">
              {areas.map((area) => (
                <li key={area.id}>
                  <Card className="flex flex-col gap-3 p-5 sm:flex-row sm:items-center sm:justify-between">
                    <div className="min-w-0 space-y-1">
                      <h3 className="break-words text-h4 text-foreground">{area.name}</h3>
                      <p className="break-all text-caption text-muted-foreground">/aidx/research/{area.slug}</p>
                      {area.description ? <p className="line-clamp-2 text-small text-foreground-secondary">{area.description}</p> : null}
                    </div>
                    <div className="flex shrink-0 gap-2">
                      <Button asChild variant="outline">
                        <Link href={`/admin/aidx/research/${area.id}/edit`}>
                          <Pencil aria-hidden="true" className="size-4" />
                          Edit<span className="sr-only"> {area.name}</span>
                        </Link>
                      </Button>
                      <Button variant="outline" onClick={() => setToDelete(area)}>
                        <Trash2 aria-hidden="true" className="size-4" />
                        Delete<span className="sr-only"> {area.name}</span>
                      </Button>
                    </div>
                  </Card>
                </li>
              ))}
            </ul>
          </section>
        ) : null}
      </div>

      <ConfirmDialog
        open={toDelete !== null}
        onOpenChange={(open) => !open && setToDelete(null)}
        title="Delete research area?"
        description="This removes the research area from every project and publication that uses it. This cannot be undone."
        confirmLabel="Delete research area"
        destructive
        busy={busy}
        onConfirm={() => void confirmDelete()}
      />
    </DashboardShell>
  );
}

function ListSkeleton() {
  return (
    <div className="space-y-3" aria-busy="true" aria-label="Loading research areas">
      {[0, 1, 2].map((row) => (
        <Card key={row} className="space-y-3 p-5">
          <Skeleton className="h-5 w-2/5" />
          <Skeleton className="h-4 w-3/5" />
        </Card>
      ))}
    </div>
  );
}
