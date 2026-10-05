"use client";

import * as React from "react";
import Link from "next/link";
import { GraduationCap, Pencil, Plus, Trash2 } from "lucide-react";
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
import { labels, RESEARCHER_CATEGORIES } from "@/lib/aidx/format";
import {
  deleteAdminPerson,
  listAdminPeople,
  type AdminPersonSummary,
  type PeopleFilters,
} from "@/lib/aidx/people-cms";
import type { PageResult, ResearcherCategory } from "@/lib/aidx/types";
import { ADMIN_NAV } from "../../_nav";
import { ConfirmDialog } from "../opportunities/_components/confirm-dialog";

const INITIAL: PeopleFilters = { search: "", category: "", published: "", page: 1, pageSize: 20 };

export default function AdminAidxPeoplePage() {
  const { getToken } = useAuth();
  const [filters, setFilters] = React.useState<PeopleFilters>(INITIAL);
  const [searchDraft, setSearchDraft] = React.useState("");
  const [result, setResult] = React.useState<PageResult<AdminPersonSummary> | null>(null);
  const [error, setError] = React.useState<AdminOpportunityError | null>(null);
  const [notice, setNotice] = React.useState<string | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [reloadKey, setReloadKey] = React.useState(0);
  const [toDelete, setToDelete] = React.useState<AdminPersonSummary | null>(null);
  const [busy, setBusy] = React.useState(false);

  React.useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      try {
        const token = await getToken();
        const page = await listAdminPeople(token, filters);
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
      await deleteAdminPerson(token, toDelete.id);
      setNotice(`"${toDelete.displayName}" was deleted.`);
      setToDelete(null);
      setReloadKey((key) => key + 1);
    } catch (caught) {
      // A refused delete (for example, publication authorship) shows the server's reason as-is.
      setError(describeAdminError(caught));
      setToDelete(null);
    } finally {
      setBusy(false);
    }
  }

  const filtersApplied = Boolean(filters.search || filters.category || filters.published);

  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="space-y-8">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div className="space-y-1">
            <p className="text-caption font-semibold uppercase tracking-wide text-primary">AIDX Lab</p>
            <h1 className="text-h2 text-foreground">Researchers and people</h1>
            <p className="max-w-2xl text-body text-muted-foreground">
              Lab members shown on the public people pages. Researchers do not sign in, and only you can manage these
              records.
            </p>
          </div>
          <Button asChild>
            <Link href="/admin/aidx/people/new">
              <Plus aria-hidden="true" className="size-4" />
              Add researcher
            </Link>
          </Button>
        </div>

        <Card className="p-5">
          <form
            role="search"
            aria-label="Filter researchers"
            className="grid gap-4 md:grid-cols-[2fr_1fr_1fr_auto] md:items-end"
            onSubmit={(event) => {
              event.preventDefault();
              setFilters((current) => ({ ...current, search: searchDraft.trim(), page: 1 }));
            }}
          >
            <FormField id="people-search" label="Search by name">
              {(props) => (
                <Input {...props} type="search" value={searchDraft} onChange={(event) => setSearchDraft(event.target.value)} placeholder="Search names" />
              )}
            </FormField>
            <FormField id="role-filter" label="Role">
              {(props) => (
                <Select
                  value={filters.category || "all"}
                  onValueChange={(next) =>
                    setFilters((current) => ({ ...current, category: next === "all" ? "" : (next as ResearcherCategory), page: 1 }))
                  }
                >
                  <SelectTrigger id={props.id}>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All roles</SelectItem>
                    {RESEARCHER_CATEGORIES.map((category) => (
                      <SelectItem key={category} value={category}>
                        {labels.researcher(category)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            </FormField>
            <FormField id="visibility-filter" label="Visibility">
              {(props) => (
                <Select
                  value={filters.published || "all"}
                  onValueChange={(next) =>
                    setFilters((current) => ({ ...current, published: next === "all" ? "" : (next as "true" | "false"), page: 1 }))
                  }
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
              icon={<GraduationCap aria-hidden="true" className="size-6" />}
              title="No people match your filters"
              description="Try a different name, or clear the filters."
              action={
                <Button
                  variant="outline"
                  onClick={() => {
                    setSearchDraft("");
                    setFilters(INITIAL);
                  }}
                >
                  Clear filters
                </Button>
              }
            />
          ) : (
            <EmptyState
              icon={<GraduationCap aria-hidden="true" className="size-6" />}
              title="No researchers yet"
              description="Add the first lab member. They stay hidden until you show them on the public pages."
              action={
                <Button asChild>
                  <Link href="/admin/aidx/people/new">Add researcher</Link>
                </Button>
              }
            />
          )
        ) : null}

        {!error && result && result.items.length > 0 ? (
          <section aria-labelledby="people-heading" aria-busy={loading} className="space-y-4">
            <div className="flex items-center justify-between gap-4">
              <h2 id="people-heading" className="text-h4 text-foreground">
                People
              </h2>
              <p className="text-small text-muted-foreground" aria-live="polite">
                {result.totalCount} {result.totalCount === 1 ? "person" : "people"}
              </p>
            </div>

            <ul className="space-y-3">
              {result.items.map((person) => (
                <li key={person.id}>
                  <Card className="flex flex-col gap-3 p-5 sm:flex-row sm:items-center sm:justify-between">
                    <div className="min-w-0 space-y-1">
                      <div className="flex flex-wrap items-center gap-3">
                        <h3 className="break-words text-h4 text-foreground">{person.displayName}</h3>
                        <Badge tone={person.published ? "success" : "neutral"} variant="subtle" showDot>
                          {person.published ? "Public" : "Hidden"}
                        </Badge>
                      </div>
                      <p className="text-small text-foreground-secondary">
                        {labels.researcher(person.category)}
                        {person.position ? ` · ${person.position}` : ""}
                      </p>
                      <p className="break-all text-caption text-muted-foreground">/aidx/people/{person.slug}</p>
                    </div>
                    <div className="flex shrink-0 gap-2">
                      <Button asChild variant="outline">
                        <Link href={`/admin/aidx/people/${person.id}/edit`}>
                          <Pencil aria-hidden="true" className="size-4" />
                          Edit<span className="sr-only"> {person.displayName}</span>
                        </Link>
                      </Button>
                      <Button variant="outline" onClick={() => setToDelete(person)}>
                        <Trash2 aria-hidden="true" className="size-4" />
                        Delete<span className="sr-only"> {person.displayName}</span>
                      </Button>
                    </div>
                  </Card>
                </li>
              ))}
            </ul>

            {result.totalPages > 1 ? (
              <nav aria-label="People pages" className="flex items-center justify-between gap-4 pt-2">
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
        title="Delete researcher?"
        description={`"${toDelete?.displayName ?? ""}" will be permanently removed, along with their team links on projects. This cannot be undone. A researcher who is an author on any publication cannot be deleted.`}
        confirmLabel="Delete researcher"
        destructive
        busy={busy}
        onConfirm={() => void confirmDelete()}
      />
    </DashboardShell>
  );
}

function ListSkeleton() {
  return (
    <div className="space-y-3" aria-busy="true" aria-label="Loading people">
      {[0, 1, 2].map((row) => (
        <Card key={row} className="space-y-3 p-5">
          <Skeleton className="h-5 w-2/5" />
          <Skeleton className="h-4 w-3/5" />
        </Card>
      ))}
    </div>
  );
}
