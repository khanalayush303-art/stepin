"use client";

import * as React from "react";
import Link from "next/link";
import { FolderKanban, Pencil, Plus } from "lucide-react";
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
import {
  archiveAdminProject,
  listAdminProjects,
  listResearchAreaOptions,
  publishAdminProject,
  type AdminProjectSummary,
  type AidxProjectStatus,
  type ProjectFilters,
} from "@/lib/aidx/cms";
import type { PageResult, ResearchArea } from "@/lib/aidx/types";
import { formatDate } from "@/lib/utils";
import { ADMIN_NAV } from "../../_nav";
import { ConfirmDialog } from "../opportunities/_components/confirm-dialog";
import { ProjectStatusBadge } from "./_components/project-status-badge";

const INITIAL: ProjectFilters = { status: "", area: "", featured: "", search: "", page: 1, pageSize: 20 };

type Pending = { kind: "publish" | "archive"; project: AdminProjectSummary } | null;

export default function AdminAidxProjectsPage() {
  const { getToken } = useAuth();
  const [filters, setFilters] = React.useState<ProjectFilters>(INITIAL);
  const [searchDraft, setSearchDraft] = React.useState("");
  const [result, setResult] = React.useState<PageResult<AdminProjectSummary> | null>(null);
  const [areas, setAreas] = React.useState<ResearchArea[]>([]);
  const [error, setError] = React.useState<AdminOpportunityError | null>(null);
  const [notice, setNotice] = React.useState<string | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [reloadKey, setReloadKey] = React.useState(0);
  const [pending, setPending] = React.useState<Pending>(null);
  const [busy, setBusy] = React.useState(false);

  React.useEffect(() => {
    listResearchAreaOptions()
      .then(setAreas)
      .catch(() => setAreas([]));
  }, []);

  React.useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      setError(null);
      try {
        const token = await getToken();
        const page = await listAdminProjects(token, filters);
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

  async function confirmAction() {
    if (!pending) return;
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const token = await getToken();
      const project = pending.project;
      if (pending.kind === "publish") {
        await publishAdminProject(token, project.id);
        setNotice(`"${project.title}" is published and visible on the AIDX projects page.`);
      } else {
        await archiveAdminProject(token, project.id);
        setNotice(`"${project.title}" is archived and no longer public.`);
      }
      setPending(null);
      setReloadKey((key) => key + 1);
    } catch (caught) {
      setError(describeAdminError(caught));
      setPending(null);
    } finally {
      setBusy(false);
    }
  }

  const filtersApplied = Boolean(filters.search || filters.status || filters.area || filters.featured);

  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="space-y-8">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div className="space-y-1">
            <p className="text-caption font-semibold uppercase tracking-wide text-primary">AIDX Lab</p>
            <h1 className="text-h2 text-foreground">Research projects</h1>
            <p className="max-w-2xl text-body text-muted-foreground">
              Projects appear on the public projects page only when published. Drafts and archived projects stay
              private.
            </p>
          </div>
          <Button asChild>
            <Link href="/admin/aidx/projects/new">
              <Plus aria-hidden="true" className="size-4" />
              Create project
            </Link>
          </Button>
        </div>

        <Card className="p-5">
          <form
            role="search"
            aria-label="Filter projects"
            className="grid gap-4 md:grid-cols-[2fr_1fr_1fr_1fr_auto] md:items-end"
            onSubmit={(event) => {
              event.preventDefault();
              setFilters((current) => ({ ...current, search: searchDraft.trim(), page: 1 }));
            }}
          >
            <FormField id="project-search" label="Search">
              {(props) => (
                <Input {...props} type="search" value={searchDraft} onChange={(event) => setSearchDraft(event.target.value)} placeholder="Title or slug" />
              )}
            </FormField>

            <FormField id="status-filter" label="Status">
              {(props) => (
                <Select
                  value={filters.status || "all"}
                  onValueChange={(next) =>
                    setFilters((current) => ({ ...current, status: next === "all" ? "" : (next as AidxProjectStatus), page: 1 }))
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

            <FormField id="area-filter" label="Research area">
              {(props) => (
                <Select
                  value={filters.area || "all"}
                  onValueChange={(next) => setFilters((current) => ({ ...current, area: next === "all" ? "" : next, page: 1 }))}
                >
                  <SelectTrigger id={props.id}>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All areas</SelectItem>
                    {areas.map((area) => (
                      <SelectItem key={area.id} value={area.id}>
                        {area.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            </FormField>

            <FormField id="featured-filter" label="Featured">
              {(props) => (
                <Select
                  value={filters.featured || "all"}
                  onValueChange={(next) =>
                    setFilters((current) => ({ ...current, featured: next === "all" ? "" : (next as "true" | "false"), page: 1 }))
                  }
                >
                  <SelectTrigger id={props.id}>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">Any</SelectItem>
                    <SelectItem value="true">Featured</SelectItem>
                    <SelectItem value="false">Not featured</SelectItem>
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
          <Alert tone={error.kind === "forbidden" ? "warning" : "error"} title="Unable to load projects">
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
              icon={<FolderKanban aria-hidden="true" className="size-6" />}
              title="No results match your current filters"
              description="Try a different search, or clear the filters."
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
              icon={<FolderKanban aria-hidden="true" className="size-6" />}
              title="No projects yet"
              description="Create the first AIDX Lab research project. It starts as a draft."
              action={
                <Button asChild>
                  <Link href="/admin/aidx/projects/new">Create project</Link>
                </Button>
              }
            />
          )
        ) : null}

        {!error && result && result.items.length > 0 ? (
          <section aria-labelledby="projects-heading" aria-busy={loading} className="space-y-4">
            <div className="flex items-center justify-between gap-4">
              <h2 id="projects-heading" className="text-h4 text-foreground">
                Projects
              </h2>
              <p className="text-small text-muted-foreground" aria-live="polite">
                {result.totalCount} {result.totalCount === 1 ? "project" : "projects"}
              </p>
            </div>

            <ul className="space-y-3">
              {result.items.map((project) => (
                <li key={project.id}>
                  <Card className="flex flex-col gap-4 p-5 lg:flex-row lg:items-start lg:justify-between">
                    <div className="min-w-0 space-y-2">
                      <div className="flex flex-wrap items-center gap-3">
                        <h3 className="break-words text-h4 text-foreground">{project.title}</h3>
                        <ProjectStatusBadge status={project.status} />
                        {project.featured ? <span className="text-caption font-semibold text-primary">Featured</span> : null}
                      </div>
                      <p className="break-all text-caption text-muted-foreground">/aidx/projects/{project.slug}</p>
                      <p className="line-clamp-2 text-small text-foreground-secondary">{project.shortDescription}</p>
                      <p className="text-small text-muted-foreground">
                        {project.researchAreas.length > 0 ? project.researchAreas.join(", ") : "No research areas"}
                        {project.updatedAt ? ` · Updated ${formatDate(project.updatedAt)}` : ""}
                      </p>
                    </div>
                    <div className="flex shrink-0 flex-wrap gap-2">
                      <Button asChild variant="outline">
                        <Link href={`/admin/aidx/projects/${project.id}/edit`}>
                          <Pencil aria-hidden="true" className="size-4" />
                          Edit<span className="sr-only"> {project.title}</span>
                        </Link>
                      </Button>
                      {project.status !== "Published" ? (
                        <Button onClick={() => setPending({ kind: "publish", project })}>
                          Publish<span className="sr-only"> {project.title}</span>
                        </Button>
                      ) : (
                        <Button variant="outline" onClick={() => setPending({ kind: "archive", project })}>
                          Archive<span className="sr-only"> {project.title}</span>
                        </Button>
                      )}
                    </div>
                  </Card>
                </li>
              ))}
            </ul>

            {result.totalPages > 1 ? (
              <nav aria-label="Project pages" className="flex items-center justify-between gap-4 pt-2">
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
        open={pending?.kind === "publish"}
        onOpenChange={(open) => !open && setPending(null)}
        title="Publish project?"
        description={`"${pending?.project.title ?? ""}" will become visible on the AIDX Lab projects page. You can archive it later.`}
        confirmLabel="Publish"
        busy={busy}
        onConfirm={() => void confirmAction()}
      />
      <ConfirmDialog
        open={pending?.kind === "archive"}
        onOpenChange={(open) => !open && setPending(null)}
        title="Archive project?"
        description={`"${pending?.project.title ?? ""}" will no longer appear in public project listings. Research opportunities linked to it stay as they are, and you can publish it again later.`}
        confirmLabel="Archive"
        busy={busy}
        onConfirm={() => void confirmAction()}
      />
    </DashboardShell>
  );
}

function ListSkeleton() {
  return (
    <div className="space-y-3" aria-busy="true" aria-label="Loading projects">
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
