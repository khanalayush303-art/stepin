"use client";

import * as React from "react";
import Link from "next/link";
import { Code2, ExternalLink, FolderGit2, Lock, Trash2 } from "lucide-react";
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
import { Textarea } from "@/components/ui/textarea";
import { CANDIDATE_NAV } from "@/components/dashboard/candidate-nav";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { listPublishedProjectOptions } from "@/lib/aidx/admin";
import type { ProjectSummary } from "@/lib/aidx/types";
import { ApiError, formError } from "@/lib/auth/client";
import { createPortfolioProject, deletePortfolioProject, listPortfolioProjects } from "@/lib/portfolio/api";
import type { PortfolioProject } from "@/lib/portfolio/types";

const NAV = CANDIDATE_NAV;
const NO_RESEARCH_PROJECT = "none";

export default function PortfolioPage() {
  const { getToken } = useAuth();
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [projects, setProjects] = React.useState<PortfolioProject[]>([]);
  const [researchProjects, setResearchProjects] = React.useState<ProjectSummary[]>([]);

  const [title, setTitle] = React.useState("");
  const [description, setDescription] = React.useState("");
  const [skills, setSkills] = React.useState("");
  const [repositoryUrl, setRepositoryUrl] = React.useState("");
  const [demoUrl, setDemoUrl] = React.useState("");
  const [researchProjectId, setResearchProjectId] = React.useState(NO_RESEARCH_PROJECT);
  const [fieldErrors, setFieldErrors] = React.useState<Record<string, string[]>>({});
  const [submitting, setSubmitting] = React.useState(false);
  const [submitError, setSubmitError] = React.useState<string>();
  const [deletingId, setDeletingId] = React.useState<string | null>(null);

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      const [loadedProjects, loadedResearch] = await Promise.all([
        listPortfolioProjects(token),
        listPublishedProjectOptions(),
      ]);
      setProjects(loadedProjects);
      setResearchProjects(loadedResearch.items);
    } catch (error) {
      setLoadError(formError(error));
    } finally {
      setLoading(false);
    }
  }, [getToken]);

  React.useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  async function onCreate(event: React.FormEvent) {
    event.preventDefault();
    setFieldErrors({});
    setSubmitError(undefined);
    setSubmitting(true);
    try {
      const token = await getToken();
      const project = await createPortfolioProject(token, {
        title: title.trim(),
        description: description.trim(),
        skills: skills
          .split(",")
          .map((s) => s.trim())
          .filter(Boolean),
        startDate: null,
        endDate: null,
        repositoryUrl: repositoryUrl.trim() || null,
        demoUrl: demoUrl.trim() || null,
        visibility: "Private",
        researchProjectId: researchProjectId === NO_RESEARCH_PROJECT ? null : researchProjectId,
      });
      setProjects((current) => [project, ...current]);
      setTitle("");
      setDescription("");
      setSkills("");
      setRepositoryUrl("");
      setDemoUrl("");
      setResearchProjectId(NO_RESEARCH_PROJECT);
    } catch (error) {
      if (error instanceof ApiError && error.problem.errors) {
        setFieldErrors(error.problem.errors);
      }
      setSubmitError(formError(error));
    } finally {
      setSubmitting(false);
    }
  }

  async function onDelete(id: string) {
    if (!window.confirm("Delete this portfolio project? This can't be undone.")) {
      return;
    }
    setDeletingId(id);
    try {
      const token = await getToken();
      await deletePortfolioProject(token, id);
      setProjects((current) => current.filter((p) => p.id !== id));
    } catch (error) {
      setSubmitError(formError(error));
    } finally {
      setDeletingId(null);
    }
  }

  return (
    <DashboardShell nav={NAV} navLabel="Applicant dashboard">
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Portfolio</h1>
          <p className="text-body text-muted-foreground">
            Evidence of your own work. New projects start Private — open one to make it Public, which is the only
            way a recruiter you&apos;ve applied to can ever see it.
          </p>
        </div>

        <Card className="p-4">
          <form onSubmit={onCreate} className="space-y-3">
            <div className="grid gap-3 sm:grid-cols-2">
              <FormField id="title" label="Title" error={fieldErrors.title?.[0]}>
                {(props) => (
                  <Input {...props} value={title} onChange={(e) => setTitle(e.target.value)} placeholder="e.g. Personal portfolio site" required />
                )}
              </FormField>
              <FormField id="skills" label="Skills (comma-separated)" error={fieldErrors.skills?.[0]}>
                {(props) => (
                  <Input {...props} value={skills} onChange={(e) => setSkills(e.target.value)} placeholder="e.g. C#, React, PostgreSQL" />
                )}
              </FormField>
            </div>

            <FormField id="description" label="Description" error={fieldErrors.description?.[0]}>
              {(props) => (
                <Textarea
                  {...props}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="What did you build, and what did you do?"
                  rows={3}
                  required
                />
              )}
            </FormField>

            <div className="grid gap-3 sm:grid-cols-3">
              <FormField id="repositoryUrl" label="Repository URL (optional)" error={fieldErrors.repositoryUrl?.[0]}>
                {(props) => (
                  <Input {...props} value={repositoryUrl} onChange={(e) => setRepositoryUrl(e.target.value)} placeholder="https://github.com/…" />
                )}
              </FormField>
              <FormField id="demoUrl" label="Demo URL (optional)" error={fieldErrors.demoUrl?.[0]}>
                {(props) => (
                  <Input {...props} value={demoUrl} onChange={(e) => setDemoUrl(e.target.value)} placeholder="https://…" />
                )}
              </FormField>
              <div>
                <span className="mb-1.5 block text-small font-medium text-foreground">Related research project (optional)</span>
                <Select value={researchProjectId} onValueChange={setResearchProjectId}>
                  <SelectTrigger>
                    <SelectValue placeholder="None" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NO_RESEARCH_PROJECT}>None</SelectItem>
                    {researchProjects.map((project) => (
                      <SelectItem key={project.id} value={project.id}>
                        {project.title}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {fieldErrors.researchProjectId?.[0] ? (
                  <p className="mt-1 text-caption text-error">{fieldErrors.researchProjectId[0]}</p>
                ) : null}
              </div>
            </div>

            <div className="flex items-center justify-between">
              {submitError ? <p className="text-small text-error">{submitError}</p> : <span />}
              <Button type="submit" disabled={submitting || !title.trim() || !description.trim()}>
                {submitting ? "Adding…" : "Add project"}
              </Button>
            </div>
          </form>
        </Card>

        {loading ? (
          <div className="space-y-4">
            <Skeleton className="h-32 w-full" />
            <Skeleton className="h-32 w-full" />
          </div>
        ) : loadError ? (
          <Alert tone="error" title="Couldn't load your portfolio">
            {loadError}
            <div className="mt-3">
              <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
                Try again
              </Button>
            </div>
          </Alert>
        ) : projects.length === 0 ? (
          <EmptyState
            icon={<FolderGit2 className="size-6" />}
            title="No portfolio projects yet"
            description="Add a project above to start building your proof-of-skills portfolio."
          />
        ) : (
          <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {projects.map((project) => (
              <li key={project.id}>
                <Card interactive className="flex h-full flex-col gap-3 p-6">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0 flex-1 space-y-1">
                      <h2 className="text-h4 text-foreground">
                        <Link
                          href={`/dashboard/portfolio/${project.id}`}
                          className="rounded-sm after:absolute after:inset-0 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                        >
                          {project.title}
                        </Link>
                      </h2>
                      {project.researchProjectTitle ? (
                        <p className="truncate text-small text-foreground-secondary">Related to: {project.researchProjectTitle}</p>
                      ) : null}
                    </div>
                    <Badge tone={project.visibility === "Public" ? "success" : "neutral"} variant="subtle" className="shrink-0">
                      {project.visibility === "Public" ? "Public" : <span className="inline-flex items-center gap-1"><Lock className="size-3" /> Private</span>}
                    </Badge>
                  </div>

                  <p className="line-clamp-3 text-small text-foreground-secondary">{project.description}</p>

                  {project.skills.length > 0 ? (
                    <div className="flex flex-wrap gap-1.5">
                      {project.skills.slice(0, 6).map((skill) => (
                        <Badge key={skill} tone="neutral" variant="subtle">
                          {skill}
                        </Badge>
                      ))}
                    </div>
                  ) : null}

                  <div className="relative z-10 mt-auto flex flex-wrap items-center gap-2 pt-2">
                    <Button size="sm" variant="outline" asChild>
                      <Link href={`/dashboard/portfolio/${project.id}`} tabIndex={-1}>
                        Edit
                      </Link>
                    </Button>
                    {project.repositoryUrl ? (
                      <Button size="sm" variant="ghost" asChild>
                        <a href={project.repositoryUrl} target="_blank" rel="noreferrer">
                          <Code2 className="size-4" />
                        </a>
                      </Button>
                    ) : null}
                    {project.demoUrl ? (
                      <Button size="sm" variant="ghost" asChild>
                        <a href={project.demoUrl} target="_blank" rel="noreferrer">
                          <ExternalLink className="size-4" />
                        </a>
                      </Button>
                    ) : null}
                    <Button
                      size="sm"
                      variant="ghost"
                      className="ml-auto text-error"
                      disabled={deletingId === project.id}
                      onClick={() => void onDelete(project.id)}
                    >
                      <Trash2 className="size-4" />
                    </Button>
                  </div>
                </Card>
              </li>
            ))}
          </ul>
        )}
      </div>
    </DashboardShell>
  );
}
