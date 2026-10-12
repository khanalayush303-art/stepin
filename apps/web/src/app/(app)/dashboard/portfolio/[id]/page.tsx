"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { ArrowLeft, Trash2 } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { CANDIDATE_NAV } from "@/components/dashboard/candidate-nav";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { listPublishedProjectOptions } from "@/lib/aidx/admin";
import type { ProjectSummary } from "@/lib/aidx/types";
import { ApiError, formError } from "@/lib/auth/client";
import { deletePortfolioProject, getPortfolioProject, updatePortfolioProject } from "@/lib/portfolio/api";
import type { PortfolioProject, PortfolioVisibility } from "@/lib/portfolio/types";

const NAV = CANDIDATE_NAV;
const NO_RESEARCH_PROJECT = "none";

export default function PortfolioProjectDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { getToken } = useAuth();
  const router = useRouter();

  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [project, setProject] = React.useState<PortfolioProject | null>(null);
  const [researchProjects, setResearchProjects] = React.useState<ProjectSummary[]>([]);

  const [title, setTitle] = React.useState("");
  const [description, setDescription] = React.useState("");
  const [skills, setSkills] = React.useState("");
  const [startDate, setStartDate] = React.useState("");
  const [endDate, setEndDate] = React.useState("");
  const [repositoryUrl, setRepositoryUrl] = React.useState("");
  const [demoUrl, setDemoUrl] = React.useState("");
  const [visibility, setVisibility] = React.useState<PortfolioVisibility>("Private");
  const [researchProjectId, setResearchProjectId] = React.useState(NO_RESEARCH_PROJECT);

  const [fieldErrors, setFieldErrors] = React.useState<Record<string, string[]>>({});
  const [saving, setSaving] = React.useState(false);
  const [saveError, setSaveError] = React.useState<string>();
  const [saved, setSaved] = React.useState(false);

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      const [loaded, loadedResearch] = await Promise.all([getPortfolioProject(token, id), listPublishedProjectOptions()]);
      setProject(loaded);
      setResearchProjects(loadedResearch.items);

      setTitle(loaded.title);
      setDescription(loaded.description);
      setSkills(loaded.skills.join(", "));
      setStartDate(loaded.startDate ?? "");
      setEndDate(loaded.endDate ?? "");
      setRepositoryUrl(loaded.repositoryUrl ?? "");
      setDemoUrl(loaded.demoUrl ?? "");
      setVisibility(loaded.visibility);
      setResearchProjectId(loaded.researchProjectId ?? NO_RESEARCH_PROJECT);
    } catch (error) {
      setLoadError(formError(error));
    } finally {
      setLoading(false);
    }
  }, [getToken, id]);

  React.useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  async function onSave(event: React.FormEvent) {
    event.preventDefault();
    setFieldErrors({});
    setSaveError(undefined);
    setSaved(false);
    setSaving(true);
    try {
      const token = await getToken();
      const updated = await updatePortfolioProject(token, id, {
        title: title.trim(),
        description: description.trim(),
        skills: skills
          .split(",")
          .map((s) => s.trim())
          .filter(Boolean),
        startDate: startDate || null,
        endDate: endDate || null,
        repositoryUrl: repositoryUrl.trim() || null,
        demoUrl: demoUrl.trim() || null,
        visibility,
        researchProjectId: researchProjectId === NO_RESEARCH_PROJECT ? null : researchProjectId,
      });
      setProject(updated);
      setSaved(true);
    } catch (error) {
      if (error instanceof ApiError && error.problem.errors) {
        setFieldErrors(error.problem.errors);
      }
      setSaveError(formError(error));
    } finally {
      setSaving(false);
    }
  }

  async function onDelete() {
    if (!window.confirm("Delete this portfolio project? This can't be undone.")) {
      return;
    }
    try {
      const token = await getToken();
      await deletePortfolioProject(token, id);
      router.push("/dashboard/portfolio");
    } catch (error) {
      setSaveError(formError(error));
    }
  }

  if (loading) {
    return (
      <DashboardShell nav={NAV} navLabel="Applicant dashboard">
        <div className="space-y-4">
          <Skeleton className="h-10 w-1/2" />
          <Skeleton className="h-96 w-full" />
        </div>
      </DashboardShell>
    );
  }

  if (loadError || !project) {
    return (
      <DashboardShell nav={NAV} navLabel="Applicant dashboard">
        <Alert tone="error" title="Couldn't load this project">
          {loadError ?? "It may not exist, or it may not be yours."}
          <div className="mt-3">
            <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
              Try again
            </Button>
          </div>
        </Alert>
      </DashboardShell>
    );
  }

  return (
    <DashboardShell nav={NAV} navLabel="Applicant dashboard">
      <div className="space-y-6">
        <Link href="/dashboard/portfolio" className="inline-flex items-center gap-1.5 text-small font-medium text-foreground-secondary hover:text-foreground">
          <ArrowLeft className="size-4" />
          All portfolio projects
        </Link>

        <Card className="space-y-4 p-6">
          <form onSubmit={onSave} className="space-y-4">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <h1 className="text-h2 text-foreground">Edit project</h1>
              <div className="flex items-center gap-3 rounded-lg border border-border bg-surface-subtle px-3 py-2">
                <div>
                  <p className="text-small font-medium text-foreground">{visibility === "Public" ? "Public" : "Private"}</p>
                  <p className="text-caption text-muted-foreground">
                    {visibility === "Public"
                      ? "Visible to recruiters you've applied to."
                      : "Only visible to you."}
                  </p>
                </div>
                <Switch
                  checked={visibility === "Public"}
                  onCheckedChange={(checked) => setVisibility(checked ? "Public" : "Private")}
                  aria-label="Toggle project visibility"
                />
              </div>
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <FormField id="title" label="Title" error={fieldErrors.title?.[0]}>
                {(props) => <Input {...props} value={title} onChange={(e) => setTitle(e.target.value)} required />}
              </FormField>
              <FormField id="skills" label="Skills (comma-separated)" error={fieldErrors.skills?.[0]}>
                {(props) => <Input {...props} value={skills} onChange={(e) => setSkills(e.target.value)} />}
              </FormField>
            </div>

            <FormField id="description" label="Description" error={fieldErrors.description?.[0]}>
              {(props) => <Textarea {...props} value={description} onChange={(e) => setDescription(e.target.value)} rows={4} required />}
            </FormField>

            <div className="grid gap-3 sm:grid-cols-2">
              <FormField id="startDate" label="Start date (optional)">
                {(props) => <Input {...props} type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />}
              </FormField>
              <FormField id="endDate" label="End date (optional)" error={fieldErrors.endDate?.[0]}>
                {(props) => <Input {...props} type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} />}
              </FormField>
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <FormField id="repositoryUrl" label="Repository URL (optional)" error={fieldErrors.repositoryUrl?.[0]}>
                {(props) => <Input {...props} value={repositoryUrl} onChange={(e) => setRepositoryUrl(e.target.value)} placeholder="https://github.com/…" />}
              </FormField>
              <FormField id="demoUrl" label="Demo URL (optional)" error={fieldErrors.demoUrl?.[0]}>
                {(props) => <Input {...props} value={demoUrl} onChange={(e) => setDemoUrl(e.target.value)} placeholder="https://…" />}
              </FormField>
            </div>

            <div>
              <span className="mb-1.5 block text-small font-medium text-foreground">Related research project (optional)</span>
              <Select value={researchProjectId} onValueChange={setResearchProjectId}>
                <SelectTrigger>
                  <SelectValue placeholder="None" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NO_RESEARCH_PROJECT}>None</SelectItem>
                  {researchProjects.map((p) => (
                    <SelectItem key={p.id} value={p.id}>
                      {p.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {fieldErrors.researchProjectId?.[0] ? <p className="mt-1 text-caption text-error">{fieldErrors.researchProjectId[0]}</p> : null}
            </div>

            {saveError ? <p className="text-small text-error">{saveError}</p> : null}
            {saved ? <p className="text-small text-success">Saved.</p> : null}

            <div className="flex flex-wrap items-center justify-between gap-2 border-t border-border pt-4">
              <Button type="submit" disabled={saving || !title.trim() || !description.trim()}>
                {saving ? "Saving…" : "Save changes"}
              </Button>
              <Button type="button" size="sm" variant="ghost" className="text-error" onClick={() => void onDelete()}>
                <Trash2 className="size-4" />
                Delete project
              </Button>
            </div>
          </form>
        </Card>
      </div>
    </DashboardShell>
  );
}
