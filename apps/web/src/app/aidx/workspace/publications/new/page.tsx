"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { PublicationForm, type PublicationFormValue, type ProjectOption } from "@/app/(app)/admin/aidx/publications/_components/publication-form";
import { emptyPublication, projectOptions, toInput } from "@/app/(app)/admin/aidx/publications/_components/mapping";
import { describeAdminError, listPublishedProjectOptions, type AdminOpportunityError } from "@/lib/aidx/admin";
import { createAuthoringPublication } from "@/lib/aidx/authoring";
import { listResearchAreaOptions } from "@/lib/aidx/cms";
import { listAdminPeople, type AdminPersonSummary } from "@/lib/aidx/people-cms";
import type { ResearchArea } from "@/lib/aidx/types";

export default function NewWorkspacePublicationPage() {
  const router = useRouter();
  const { getToken } = useAuth();
  const [value, setValue] = React.useState<PublicationFormValue>(emptyPublication);
  const [areas, setAreas] = React.useState<ResearchArea[]>([]);
  const [researchers, setResearchers] = React.useState<AdminPersonSummary[]>([]);
  const [projects, setProjects] = React.useState<ProjectOption[]>([]);
  const [saving, setSaving] = React.useState(false);
  const [error, setError] = React.useState<AdminOpportunityError | null>(null);

  React.useEffect(() => {
    let cancelled = false;
    async function loadOptions() {
      const token = await getToken();
      const [areaList, people, publishedProjects] = await Promise.all([
        listResearchAreaOptions().catch(() => [] as ResearchArea[]),
        listAdminPeople(token, { pageSize: 100 }).then((p) => p.items).catch(() => [] as AdminPersonSummary[]),
        listPublishedProjectOptions().then((p) => p.items).catch(() => []),
      ]);
      if (cancelled) return;
      setAreas(areaList);
      setResearchers(people);
      setProjects(projectOptions(publishedProjects, []));
    }
    void loadOptions();
    return () => {
      cancelled = true;
    };
  }, [getToken]);

  async function onSave() {
    setSaving(true);
    setError(null);
    try {
      const token = await getToken();
      const created = await createAuthoringPublication(token, toInput(value, null));
      router.push(`/aidx/workspace/publications/${created.id}/edit`);
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
        <span className="text-foreground">New publication</span>
      </nav>

      <div className="space-y-1">
        <h1 className="text-h2 text-foreground">Add a publication</h1>
        <p className="text-body text-muted-foreground">Saved as your own draft until you submit it for review.</p>
      </div>

      {error ? (
        <Alert tone="error" title={error.kind === "validation" ? "Check the form" : "Couldn't add this publication"}>
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
        <PublicationForm value={value} onChange={setValue} fieldErrors={fieldErrors} disabled={saving} areas={areas} researchers={researchers} projects={projects} />
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
