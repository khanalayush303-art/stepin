"use client";

import * as React from "react";
import Link from "next/link";
import { Lightbulb, Pencil, Plus } from "lucide-react";
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
import {
  describeAdminError,
  listAdminOpportunities,
  listPublishedProjectOptions,
  type AdminOpportunityError,
  type AidxOpportunityAdmin,
  type AidxOpportunityStatus,
  type OpportunityFilters,
} from "@/lib/aidx/admin";
import type { PageResult, ProjectSummary } from "@/lib/aidx/types";
import { formatDate } from "@/lib/utils";
import { ADMIN_NAV } from "../../_nav";
import { OpportunityStatusBadge } from "./_components/opportunity-status-badge";

const STATUS_OPTIONS: { value: AidxOpportunityStatus | "all"; label: string }[] = [
  { value: "all", label: "All statuses" },
  { value: "Draft", label: "Draft" },
  { value: "Published", label: "Published" },
  { value: "Unpublished", label: "Unpublished" },
];

const INITIAL: OpportunityFilters = { status: "", project: "", search: "", page: 1, pageSize: 20 };

export default function AdminAidxOpportunitiesPage() {
  const { getToken } = useAuth();
  const [filters, setFilters] = React.useState<OpportunityFilters>(INITIAL);
  const [searchDraft, setSearchDraft] = React.useState("");
  const [result, setResult] = React.useState<PageResult<AidxOpportunityAdmin> | null>(null);
  const [error, setError] = React.useState<AdminOpportunityError | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [reloadKey, setReloadKey] = React.useState(0);
  const [projects, setProjects] = React.useState<ProjectSummary[]>([]);

  React.useEffect(() => {
    let cancelled = false;
    listPublishedProjectOptions()
      .then((page) => {
        if (!cancelled) setProjects(page.items);
      })
      .catch(() => {
        // The filter still works without project names. The list itself reports load failures.
      });
    return () => {
      cancelled = true;
    };
  }, []);

  React.useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      setError(null);
      try {
        const token = await getToken();
        const page = await listAdminOpportunities(token, filters);
        if (!cancelled) setResult(page);
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

  const filtersApplied = Boolean(filters.search || filters.status || filters.project);

  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="space-y-8">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div className="space-y-1">
            <p className="text-caption font-semibold uppercase tracking-wide text-primary">AIDX Lab</p>
            <h1 className="text-h2 text-foreground">Research opportunities</h1>
            <p className="max-w-2xl text-body text-muted-foreground">
              Research roles published to the AIDX Lab opportunities page. Each one is a StepIn job, so candidates apply
              through the existing application process.
            </p>
          </div>
          <Button asChild>
            <Link href="/admin/aidx/opportunities/new">
              <Plus aria-hidden="true" className="size-4" />
              Create research opportunity
            </Link>
          </Button>
        </div>

        <Card className="p-5">
          <form
            role="search"
            aria-label="Filter research opportunities"
            className="grid gap-4 md:grid-cols-[2fr_1fr_1fr_auto] md:items-end"
            onSubmit={(event) => {
              event.preventDefault();
              setFilters((current) => ({ ...current, search: searchDraft.trim(), page: 1 }));
            }}
          >
            <FormField id="search" label="Search by title">
              {(props) => (
                <Input
                  {...props}
                  type="search"
                  value={searchDraft}
                  onChange={(event) => setSearchDraft(event.target.value)}
                  placeholder="Search research roles"
                />
              )}
            </FormField>

            <FormField id="status-filter" label="Status">
              {(props) => (
                <Select
                  value={filters.status || "all"}
                  onValueChange={(next) =>
                    setFilters((current) => ({ ...current, status: next === "all" ? "" : (next as AidxOpportunityStatus), page: 1 }))
                  }
                >
                  <SelectTrigger id={props.id}>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {STATUS_OPTIONS.map((option) => (
                      <SelectItem key={option.value} value={option.value}>
                        {option.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            </FormField>

            <FormField id="project-filter" label="Project">
              {(props) => (
                <Select
                  value={filters.project || "all"}
                  onValueChange={(next) =>
                    setFilters((current) => ({ ...current, project: next === "all" ? "" : next, page: 1 }))
                  }
                >
                  <SelectTrigger id={props.id}>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All projects</SelectItem>
                    {projects.map((project) => (
                      <SelectItem key={project.id} value={project.id}>
                        {project.title}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            </FormField>

            <Button type="submit" variant="outline">
              Search
            </Button>
          </form>
        </Card>

        {error ? <ErrorPanel error={error} onRetry={() => setReloadKey((key) => key + 1)} /> : null}

        {loading && !result ? <ListSkeleton /> : null}

        {!error && result && result.items.length === 0 ? (
          filtersApplied ? (
            <EmptyState
              icon={<Lightbulb aria-hidden="true" className="size-6" />}
              title="No opportunities match your current filters"
              description="Try a different search, or clear the filters to see everything."
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
              icon={<Lightbulb aria-hidden="true" className="size-6" />}
              title="No research opportunities yet"
              description="Create the first AIDX Lab research opportunity. It starts as a draft and is not public until you publish it."
              action={
                <Button asChild>
                  <Link href="/admin/aidx/opportunities/new">Create research opportunity</Link>
                </Button>
              }
            />
          )
        ) : null}

        {!error && result && result.items.length > 0 ? (
          <section aria-labelledby="results-heading" aria-busy={loading} className="space-y-4">
            <div className="flex items-center justify-between gap-4">
              <h2 id="results-heading" className="text-h4 text-foreground">
                Opportunities
              </h2>
              <p className="text-small text-muted-foreground" aria-live="polite">
                {result.totalCount} {result.totalCount === 1 ? "opportunity" : "opportunities"}
              </p>
            </div>

            <ul className="space-y-3">
              {result.items.map((item) => (
                <li key={item.id}>
                  <Card className="flex flex-col gap-3 p-5 sm:flex-row sm:items-center sm:justify-between">
                    <div className="min-w-0 space-y-2">
                      <div className="flex flex-wrap items-center gap-3">
                        <h3 className="break-words text-h4 text-foreground">{item.title}</h3>
                        <OpportunityStatusBadge status={item.status} />
                      </div>
                      <p className="text-small text-muted-foreground">
                        {item.location} · {labelFor(item.employmentType)} · {labelFor(item.workplaceType)}
                      </p>
                      <p className="text-small text-foreground-secondary">
                        Project: {item.aidxProjectTitle ?? "No project"}
                        {item.updatedAt ? ` · Updated ${formatDate(item.updatedAt)}` : ""}
                      </p>
                    </div>
                    <Button asChild variant="outline" className="shrink-0">
                      <Link href={`/admin/aidx/opportunities/${item.id}/edit`}>
                        <Pencil aria-hidden="true" className="size-4" />
                        Edit<span className="sr-only"> {item.title}</span>
                      </Link>
                    </Button>
                  </Card>
                </li>
              ))}
            </ul>

            {result.totalPages > 1 ? (
              <nav aria-label="Opportunity pages" className="flex items-center justify-between gap-4 pt-2">
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
    </DashboardShell>
  );
}

function labelFor(value: string): string {
  const labels: Record<string, string> = {
    FullTime: "Full-time",
    PartTime: "Part-time",
    Contract: "Contract",
    Internship: "Internship",
    Casual: "Casual",
    OnSite: "On-site",
    Hybrid: "Hybrid",
    Remote: "Remote",
  };
  return labels[value] ?? value;
}

function ErrorPanel({ error, onRetry }: { error: AdminOpportunityError; onRetry: () => void }) {
  if (error.kind === "unauthenticated") {
    return (
      <Alert tone="warning" title="Sign in to continue">
        {error.message}{" "}
        <Link href="/sign-in?returnTo=/admin/aidx/opportunities" className="font-semibold underline underline-offset-4">
          Sign in
        </Link>
      </Alert>
    );
  }

  if (error.kind === "forbidden") {
    return (
      <Alert tone="warning" title="Access denied">
        {error.message}
      </Alert>
    );
  }

  return (
    <Alert tone="error" title="Unable to load research opportunities">
      <p>{error.message}</p>
      <Button variant="outline" className="mt-3" onClick={onRetry}>
        Try again
      </Button>
    </Alert>
  );
}

function ListSkeleton() {
  return (
    <div className="space-y-3" aria-busy="true" aria-label="Loading research opportunities">
      {[0, 1, 2].map((row) => (
        <Card key={row} className="space-y-3 p-5">
          <Skeleton className="h-5 w-2/5" />
          <Skeleton className="h-4 w-3/5" />
          <Skeleton className="h-4 w-1/3" />
        </Card>
      ))}
    </div>
  );
}
