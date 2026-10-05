"use client";

import * as React from "react";
import Link from "next/link";
import { CalendarDays, Pencil, Plus, Trash2 } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { describeAdminError, type AdminOpportunityError } from "@/lib/aidx/admin";
import { formatDateTime } from "@/lib/aidx/format";
import {
  deleteAdminEvent,
  listAdminEvents,
  type AdminEventSummary,
  type AidxEventStatus,
  type EventFilters,
} from "@/lib/aidx/events-cms";
import type { PageResult } from "@/lib/aidx/types";
import { ADMIN_NAV } from "../../_nav";
import { ConfirmDialog } from "../opportunities/_components/confirm-dialog";
import { ProjectStatusBadge } from "../projects/_components/project-status-badge";

const INITIAL: EventFilters = { q: "", status: "", upcoming: false, page: 1, pageSize: 20 };

export default function AdminAidxEventsPage() {
  const { getToken } = useAuth();
  const [filters, setFilters] = React.useState<EventFilters>(INITIAL);
  const [searchDraft, setSearchDraft] = React.useState("");
  const [result, setResult] = React.useState<PageResult<AdminEventSummary> | null>(null);
  const [error, setError] = React.useState<AdminOpportunityError | null>(null);
  const [notice, setNotice] = React.useState<string | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [reloadKey, setReloadKey] = React.useState(0);
  const [toDelete, setToDelete] = React.useState<AdminEventSummary | null>(null);
  const [busy, setBusy] = React.useState(false);

  React.useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      try {
        const token = await getToken();
        const page = await listAdminEvents(token, filters);
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
      await deleteAdminEvent(token, toDelete.id);
      setNotice(`"${toDelete.title}" was deleted.`);
      setToDelete(null);
      setReloadKey((key) => key + 1);
    } catch (caught) {
      // A refused delete (published or archived event) shows the server's reason as written.
      setError(describeAdminError(caught));
      setToDelete(null);
    } finally {
      setBusy(false);
    }
  }

  const filtersApplied = Boolean(filters.q || filters.status || filters.upcoming);

  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="space-y-8">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div className="space-y-1">
            <p className="text-caption font-semibold uppercase tracking-wide text-primary">AIDX Lab</p>
            <h1 className="text-h2 text-foreground">Events</h1>
            <p className="max-w-2xl text-body text-muted-foreground">
              Talks, workshops and other lab events. Only published events appear on the public events page. Archive an
              event to take it down. Its address then stops working for anyone who has the link.
            </p>
          </div>
          <Button asChild>
            <Link href="/admin/aidx/events/new">
              <Plus aria-hidden="true" className="size-4" />
              Add event
            </Link>
          </Button>
        </div>

        <Card className="p-5">
          <form
            role="search"
            aria-label="Filter events"
            className="grid gap-4 md:grid-cols-[2fr_1fr_1fr_auto] md:items-end"
            onSubmit={(event) => {
              event.preventDefault();
              setFilters((current) => ({ ...current, q: searchDraft.trim(), page: 1 }));
            }}
          >
            <FormField id="event-search" label="Search title, description or location">
              {(props) => (
                <Input {...props} type="search" value={searchDraft} onChange={(event) => setSearchDraft(event.target.value)} placeholder="Keyword" />
              )}
            </FormField>
            <FormField id="event-status" label="Status">
              {(props) => (
                <Select
                  value={filters.status || "all"}
                  onValueChange={(next) =>
                    setFilters((current) => ({ ...current, status: next === "all" ? "" : (next as AidxEventStatus), page: 1 }))
                  }
                >
                  <SelectTrigger id={props.id}>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All statuses</SelectItem>
                    <SelectItem value="Draft">Draft</SelectItem>
                    <SelectItem value="Published">Published</SelectItem>
                    <SelectItem value="Archived">Archived</SelectItem>
                  </SelectContent>
                </Select>
              )}
            </FormField>
            <FormField id="event-when" label="Dates">
              {(props) => (
                <Select
                  value={filters.upcoming ? "upcoming" : "all"}
                  onValueChange={(next) => setFilters((current) => ({ ...current, upcoming: next === "upcoming", page: 1 }))}
                >
                  <SelectTrigger id={props.id}>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All dates</SelectItem>
                    <SelectItem value="upcoming">Upcoming only</SelectItem>
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
              icon={<CalendarDays aria-hidden="true" className="size-6" />}
              title="No events match your filters"
              description="Try a different keyword or status, or clear the filters."
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
              icon={<CalendarDays aria-hidden="true" className="size-6" />}
              title="No events yet"
              description="Add the first event. It starts as a draft and is not public until you publish it."
              action={
                <Button asChild>
                  <Link href="/admin/aidx/events/new">Add event</Link>
                </Button>
              }
            />
          )
        ) : null}

        {!error && result && result.items.length > 0 ? (
          <section aria-labelledby="events-heading" aria-busy={loading} className="space-y-4">
            <div className="flex items-center justify-between gap-4">
              <h2 id="events-heading" className="text-h4 text-foreground">
                Events
              </h2>
              <p className="text-small text-muted-foreground" aria-live="polite">
                {result.totalCount} {result.totalCount === 1 ? "event" : "events"}
              </p>
            </div>

            <ul className="space-y-3">
              {result.items.map((item) => (
                <li key={item.id}>
                  <Card className="flex flex-col gap-3 p-5 sm:flex-row sm:items-start sm:justify-between">
                    <div className="min-w-0 space-y-2">
                      <div className="flex flex-wrap items-center gap-3">
                        <h3 className="break-words text-h4 text-foreground">{item.title}</h3>
                        <ProjectStatusBadge status={item.status} />
                      </div>
                      <p className="text-small text-foreground-secondary">
                        <time dateTime={item.startsAt}>{formatDateTime(item.startsAt)}</time>
                        {item.location ? ` · ${item.location}` : ""}
                        {item.speakerName ? ` · ${item.speakerName}` : ""}
                      </p>
                      <p className="break-all text-caption text-muted-foreground">/aidx/events/{item.slug}</p>
                    </div>
                    <div className="flex shrink-0 gap-2">
                      <Button asChild variant="outline">
                        <Link href={`/admin/aidx/events/${item.id}/edit`}>
                          <Pencil aria-hidden="true" className="size-4" />
                          Edit<span className="sr-only"> {item.title}</span>
                        </Link>
                      </Button>
                      {item.status === "Draft" ? (
                        <Button variant="outline" onClick={() => setToDelete(item)}>
                          <Trash2 aria-hidden="true" className="size-4" />
                          Delete<span className="sr-only"> {item.title}</span>
                        </Button>
                      ) : null}
                    </div>
                  </Card>
                </li>
              ))}
            </ul>

            {result.totalPages > 1 ? (
              <nav aria-label="Event pages" className="flex items-center justify-between gap-4 pt-2">
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
        title="Delete draft event?"
        description={`"${toDelete?.title ?? ""}" will be permanently removed. This cannot be undone.`}
        confirmLabel="Delete draft"
        destructive
        busy={busy}
        onConfirm={() => void confirmDelete()}
      />
    </DashboardShell>
  );
}

function ListSkeleton() {
  return (
    <div className="space-y-3" aria-busy="true" aria-label="Loading events">
      {[0, 1, 2].map((row) => (
        <Card key={row} className="space-y-3 p-5">
          <Skeleton className="h-5 w-2/5" />
          <Skeleton className="h-4 w-3/5" />
        </Card>
      ))}
    </div>
  );
}
