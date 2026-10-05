"use client";

import * as React from "react";
import Link from "next/link";
import { Library, Pencil, Plus, Trash2 } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { describeAdminError, type AdminOpportunityError } from "@/lib/aidx/admin";
import { labels, PUBLICATION_TYPES } from "@/lib/aidx/format";
import {
  deleteAdminPublication,
  listAdminPublications,
  type AdminPublicationSummary,
  type PublicationFilters,
} from "@/lib/aidx/publications-cms";
import type { PageResult, PublicationType } from "@/lib/aidx/types";
import { ADMIN_NAV } from "../../_nav";
import { ConfirmDialog } from "../opportunities/_components/confirm-dialog";

const INITIAL: PublicationFilters = { q: "", type: "", year: "", published: "", page: 1, pageSize: 20 };

export default function AdminAidxPublicationsPage() {
  const { getToken } = useAuth();
  const [filters, setFilters] = React.useState<PublicationFilters>(INITIAL);
  const [draft, setDraft] = React.useState({ q: "", year: "" });
  const [result, setResult] = React.useState<PageResult<AdminPublicationSummary> | null>(null);
  const [error, setError] = React.useState<AdminOpportunityError | null>(null);
  const [notice, setNotice] = React.useState<string | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [reloadKey, setReloadKey] = React.useState(0);
  const [toDelete, setToDelete] = React.useState<AdminPublicationSummary | null>(null);
  const [busy, setBusy] = React.useState(false);

  React.useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      try {
        const token = await getToken();
        const page = await listAdminPublications(token, filters);
        if (!cancelled) {
          setResult(page);
          setError(null);
        }
      } catch (caught) {
        if (!cancelled) setError(describeAdminError(caught));
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    void load();
    return () => {
      cancelled = true;
    };
  }, [filters, reloadKey, getToken]);

  async function confirmDelete() {
    if (!toDelete) return;
    setBusy(true);
    try {
      const token = await getToken();
      await deleteAdminPublication(token, toDelete.id);
      setNotice(`"${toDelete.title}" was deleted.`);
      setToDelete(null);
      setReloadKey((key) => key + 1);
    } catch (caught) {
      setError(describeAdminError(caught));
      setToDelete(null);
    } finally {
      setBusy(false);
    }
  }

  function applySearch(event: React.FormEvent) {
    event.preventDefault();
    setFilters((current) => ({
      ...current,
      q: draft.q.trim(),
      year: draft.year.trim(),
      page: 1,
    }));
  }

  const filtersApplied = Boolean(filters.q || filters.type || filters.year || filters.published);

  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="space-y-8">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div className="space-y-1">
            <p className="text-caption font-semibold uppercase tracking-wide text-primary">AIDX Lab</p>
            <h1 className="text-h2 text-foreground">Publications</h1>
            <p className="max-w-2xl text-body text-muted-foreground">
              Papers, reports and datasets from the lab. Only publications marked public appear on the publications page.
            </p>
          </div>
          <Button asChild>
            <Link href="/admin/aidx/publications/new">
              <Plus aria-hidden="true" className="size-4" />
              Add publication
            </Link>
          </Button>
        </div>

        <Card className="p-5">
          <form role="search" aria-label="Filter publications" className="grid gap-4 md:grid-cols-[2fr_1fr_1fr_1fr_auto] md:items-end" onSubmit={applySearch}>
            <FormField id="pub-search" label="Search title or abstract">
              {(props) => (
                <Input {...props} type="search" value={draft.q} onChange={(event) => setDraft({ ...draft, q: event.target.value })} placeholder="Keyword" />
              )}
            </FormField>
            <FormField id="pub-year" label="Year">
              {(props) => (
                <Input
                  {...props}
                  type="number"
                  inputMode="numeric"
                  min={1900}
                  max={2100}
                  value={draft.year}
                  onChange={(event) => setDraft({ ...draft, year: event.target.value })}
                  placeholder="Any year"
                />
              )}
            </FormField>
            <FormField id="pub-type" label="Type">
              {(props) => (
                <Select
                  value={filters.type || "all"}
                  onValueChange={(next) => setFilters((current) => ({ ...current, type: next === "all" ? "" : (next as PublicationType), page: 1 }))}
                >
                  <SelectTrigger id={props.id}>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All types</SelectItem>
                    {PUBLICATION_TYPES.map((type) => (
                      <SelectItem key={type} value={type}>
                        {labels.publication(type)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            </FormField>
            <FormField id="pub-visibility" label="Visibility">
              {(props) => (
                <Select
                  value={filters.published || "all"}
                  onValueChange={(next) => setFilters((current) => ({ ...current, published: next === "all" ? "" : (next as "true" | "false"), page: 1 }))}
                >
                  <SelectTrigger id={props.id}>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">Public and hidden</SelectItem>
                    <SelectItem value="true">Public only</SelectItem>
                    <SelectItem value="false">Hidden only</SelectItem>
                  </SelectContent>
                </Select>
              )}
            </FormField>
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
          <Alert tone={error.kind === "forbidden" ? "warning" : "error"} title="Unable to complete that">
            <p>{error.message}</p>
            {error.kind !== "forbidden" ? (
              <Button variant="outline" className="mt-3" onClick={() => setReloadKey((key) => key + 1)}>
                Try again
              </Button>
            ) : null}
          </Alert>
        ) : null}

        {loading && !result ? <ListSkeleton /> : null}

        {!error && result && result.items.length === 0 ? (
          filtersApplied ? (
            <EmptyState
              icon={<Library aria-hidden="true" className="size-6" />}
              title="No publications match your filters"
              description="Try a different keyword, year or type, or clear the filters."
              action={
                <Button
                  variant="outline"
                  onClick={() => {
                    setDraft({ q: "", year: "" });
                    setFilters(INITIAL);
                  }}
                >
                  Clear filters
                </Button>
              }
            />
          ) : (
            <EmptyState
              icon={<Library aria-hidden="true" className="size-6" />}
              title="No publications yet"
              description="Add the first publication. It stays hidden until you show it on the public page."
              action={
                <Button asChild>
                  <Link href="/admin/aidx/publications/new">Add publication</Link>
                </Button>
              }
            />
          )
        ) : null}

        {!error && result && result.items.length > 0 ? (
          <section aria-labelledby="publications-heading" aria-busy={loading} className="space-y-4">
            <div className="flex items-center justify-between gap-4">
              <h2 id="publications-heading" className="text-h4 text-foreground">
                Publications
              </h2>
              <p className="text-small text-muted-foreground" aria-live="polite">
                {result.totalCount} {result.totalCount === 1 ? "publication" : "publications"}
              </p>
            </div>

            <ul className="space-y-3">
              {result.items.map((publication) => (
                <li key={publication.id}>
                  <Card className="flex flex-col gap-3 p-5 sm:flex-row sm:items-start sm:justify-between">
                    <div className="min-w-0 space-y-2">
                      <div className="flex flex-wrap items-center gap-3">
                        <h3 className="break-words text-h4 text-foreground">{publication.title}</h3>
                        <Badge tone={publication.published ? "success" : "neutral"} variant="subtle" showDot>
                          {publication.published ? "Public" : "Hidden"}
                        </Badge>
                      </div>
                      <p className="text-small text-foreground-secondary">
                        {labels.publication(publication.publicationType)} · {publication.year}
                        {publication.venue ? ` · ${publication.venue}` : ""}
                      </p>
                      {publication.authors.length > 0 ? (
                        <p className="break-words text-small text-muted-foreground">{publication.authors.join(", ")}</p>
                      ) : (
                        <p className="text-small text-muted-foreground">No authors listed</p>
                      )}
                    </div>
                    <div className="flex shrink-0 gap-2">
                      <Button asChild variant="outline">
                        <Link href={`/admin/aidx/publications/${publication.id}/edit`}>
                          <Pencil aria-hidden="true" className="size-4" />
                          Edit<span className="sr-only"> {publication.title}</span>
                        </Link>
                      </Button>
                      <Button variant="outline" onClick={() => setToDelete(publication)}>
                        <Trash2 aria-hidden="true" className="size-4" />
                        Delete<span className="sr-only"> {publication.title}</span>
                      </Button>
                    </div>
                  </Card>
                </li>
              ))}
            </ul>

            {result.totalPages > 1 ? (
              <nav aria-label="Publication pages" className="flex items-center justify-between gap-4 pt-2">
                <Button
                  variant="outline"
                  disabled={result.page <= 1 || loading}
                  onClick={() => setFilters((current) => ({ ...current, page: (current.page ?? 1) - 1 }))}
                >
                  Previous
                </Button>
                <p className="text-small text-muted-foreground">
                  Page {result.page} of {result.totalPages}
                </p>
                <Button
                  variant="outline"
                  disabled={result.page >= result.totalPages || loading}
                  onClick={() => setFilters((current) => ({ ...current, page: (current.page ?? 1) + 1 }))}
                >
                  Next
                </Button>
              </nav>
            ) : null}
          </section>
        ) : null}
      </div>

      <ConfirmDialog
        open={toDelete !== null}
        onOpenChange={(open) => !open && setToDelete(null)}
        title="Delete publication?"
        description={`"${toDelete?.title ?? ""}" will be permanently removed, with its author and link records. Researchers, areas and projects are kept. This cannot be undone.`}
        confirmLabel="Delete publication"
        destructive
        busy={busy}
        onConfirm={() => void confirmDelete()}
      />
    </DashboardShell>
  );
}

function ListSkeleton() {
  return (
    <div className="space-y-3" aria-busy="true" aria-label="Loading publications">
      {[0, 1, 2].map((row) => (
        <Card key={row} className="space-y-3 p-5">
          <Skeleton className="h-5 w-2/5" />
          <Skeleton className="h-4 w-3/5" />
        </Card>
      ))}
    </div>
  );
}
