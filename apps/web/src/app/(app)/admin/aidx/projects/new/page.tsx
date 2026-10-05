"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { describeAdminError, type AdminOpportunityError } from "@/lib/aidx/admin";
import { createAdminProject, listResearchAreaOptions, type ProjectInput } from "@/lib/aidx/cms";
import type { ResearchArea } from "@/lib/aidx/types";
import { ADMIN_NAV } from "../../../_nav";
import { ProjectForm } from "../_components/project-form";

const EMPTY: ProjectInput = {
  title: "",
  slug: null,
  shortDescription: "",
  description: "",
  startDate: null,
  endDate: null,
  externalUrl: null,
  featured: false,
  researchAreaIds: [],
  technologies: [],
  researchers: [],
};

export default function NewAidxProjectPage() {
  const router = useRouter();
  const { getToken } = useAuth();
  const [value, setValue] = React.useState<ProjectInput>(EMPTY);
  const [areas, setAreas] = React.useState<ResearchArea[]>([]);
  const [saving, setSaving] = React.useState(false);
  const [error, setError] = React.useState<AdminOpportunityError | null>(null);

  React.useEffect(() => {
    listResearchAreaOptions()
      .then(setAreas)
      .catch(() => setAreas([]));
  }, []);

  async function onSave() {
    setSaving(true);
    setError(null);
    try {
      const token = await getToken();
      const created = await createAdminProject(token, value);
      router.push(`/admin/aidx/projects/${created.id}/edit`);
    } catch (caught) {
      setError(describeAdminError(caught));
      setSaving(false);
    }
  }

  const fieldErrors = error?.kind === "validation" ? error.fields : {};

  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="max-w-3xl space-y-6">
        <nav aria-label="Breadcrumb" className="text-small text-muted-foreground">
          <Link href="/admin/aidx/projects" className="underline-offset-4 hover:underline">
            Research projects
          </Link>
          <span aria-hidden="true"> / </span>
          <span className="text-foreground">New</span>
        </nav>

        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Create project</h1>
          <p className="text-body text-muted-foreground">Saved as a draft. It stays private until you publish it.</p>
        </div>

        {error ? (
          <Alert tone="error" title={error.kind === "validation" ? "Check the form" : "Couldn't create this project"}>
            {error.message}
          </Alert>
        ) : null}

        <form
          onSubmit={(event) => {
            event.preventDefault();
            void onSave();
          }}
          className="space-y-6"
        >
          <ProjectForm value={value} onChange={setValue} fieldErrors={fieldErrors} disabled={saving} areas={areas} researcherNames={[]} />
          <div className="flex justify-end gap-3">
            <Button variant="outline" asChild>
              <Link href="/admin/aidx/projects">Cancel</Link>
            </Button>
            <Button type="submit" size="lg" loading={saving} loadingText="Saving…">
              Create draft
            </Button>
          </div>
        </form>
      </div>
    </DashboardShell>
  );
}
