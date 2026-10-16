"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { AidxResearcherPickerOption } from "@/components/aidx/aidx-researcher-picker";
import { createAuthoringProject } from "@/lib/aidx/authoring";
import { describeAdminError, type AdminOpportunityError } from "@/lib/aidx/admin";
import { listResearchAreaOptions, listTechnologyOptions, type ProjectInput } from "@/lib/aidx/cms";
import { listAdminPeople } from "@/lib/aidx/people-cms";
import type { ResearchArea } from "@/lib/aidx/types";
import { ProjectForm } from "@/app/(app)/admin/aidx/projects/_components/project-form";

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
  outcomes: null,
  milestones: [],
  expectedVersion: null,
};

export default function NewWorkspaceProjectPage() {
  const router = useRouter();
  const { getToken } = useAuth();
  const [value, setValue] = React.useState<ProjectInput>(EMPTY);
  const [areas, setAreas] = React.useState<ResearchArea[]>([]);
  const [researchers, setResearchers] = React.useState<AidxResearcherPickerOption[]>([]);
  const [technologySuggestions, setTechnologySuggestions] = React.useState<string[]>([]);
  const [saving, setSaving] = React.useState(false);
  const [error, setError] = React.useState<AdminOpportunityError | null>(null);

  React.useEffect(() => {
    listResearchAreaOptions().then(setAreas).catch(() => setAreas([]));
    listTechnologyOptions().then(setTechnologySuggestions).catch(() => setTechnologySuggestions([]));
    void getToken().then((token) =>
      listAdminPeople(token, { pageSize: 200 })
        .then((p) => setResearchers(p.items.map((r) => ({ id: r.id, displayName: r.displayName }))))
        .catch(() => setResearchers([]))
    );
  }, [getToken]);

  async function onSave() {
    setSaving(true);
    setError(null);
    try {
      const token = await getToken();
      const created = await createAuthoringProject(token, value);
      router.push(`/aidx/workspace/projects/${created.id}/edit`);
    } catch (caught) {
      setError(describeAdminError(caught));
      setSaving(false);
    }
  }

  const fieldErrors = error?.kind === "validation" ? error.fields : {};

  return (
    <div className="container-page max-w-3xl space-y-6 py-10">
      <nav aria-label="Breadcrumb" className="text-small text-muted-foreground">
        <Link href="/aidx/workspace" className="underline-offset-4 hover:underline">
          My AIDX workspace
        </Link>
        <span aria-hidden="true"> / </span>
        <span className="text-foreground">New project</span>
      </nav>

      <div className="space-y-1">
        <h1 className="text-h2 text-foreground">Create a project</h1>
        <p className="text-body text-muted-foreground">
          Saved as your own draft. It stays private to you and the AIDX team until you submit it for review and it is
          approved.
        </p>
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
        <ProjectForm
          value={value}
          onChange={setValue}
          fieldErrors={fieldErrors}
          disabled={saving}
          areas={areas}
          researchers={researchers}
          technologySuggestions={technologySuggestions}
        />
        <div className="flex justify-end gap-3">
          <Button variant="outline" asChild>
            <Link href="/aidx/workspace">Cancel</Link>
          </Button>
          <Button type="submit" size="lg" loading={saving} loadingText="Saving…">
            Save draft
          </Button>
        </div>
      </form>
    </div>
  );
}
