"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { describeAdminError, listPublishedProjectOptions, type AdminOpportunityError } from "@/lib/aidx/admin";
import { listResearchAreaOptions } from "@/lib/aidx/cms";
import { createAdminPublication } from "@/lib/aidx/publications-cms";
import { listAdminPeople, type AdminPersonSummary } from "@/lib/aidx/people-cms";
import type { ResearchArea } from "@/lib/aidx/types";
import { ADMIN_NAV } from "../../../_nav";
import { PublicationForm, type PublicationFormValue, type ProjectOption } from "../_components/publication-form";
import { emptyPublication, projectOptions, toInput } from "../_components/mapping";

export default function NewPublicationPage() {
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
      const created = await createAdminPublication(token, toInput(value));
      router.push(`/admin/aidx/publications/${created.id}/edit`);
    } catch (caught) {
      // Values stay in the form, so a validation error does not cost the admin their work.
      setError(describeAdminError(caught));
      setSaving(false);
    }
  }

  const fieldErrors = error?.kind === "validation" ? error.fields : {};

  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="max-w-3xl space-y-6">
        <nav aria-label="Breadcrumb" className="text-small text-muted-foreground">
          <Link href="/admin/aidx/publications" className="underline-offset-4 hover:underline">
            Publications
          </Link>
          <span aria-hidden="true"> / </span>
          <span className="text-foreground">New</span>
        </nav>

        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Add publication</h1>
          <p className="text-body text-muted-foreground">New publications start hidden. Turn on the public toggle when they are ready.</p>
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
          <PublicationForm
            value={value}
            onChange={setValue}
            fieldErrors={fieldErrors}
            disabled={saving}
            areas={areas}
            researchers={researchers}
            projects={projects}
          />
          <div className="flex justify-end gap-3">
            <Button variant="outline" asChild>
              <Link href="/admin/aidx/publications">Cancel</Link>
            </Button>
            <Button type="submit" size="lg" loading={saving} loadingText="Saving…">
              Add publication
            </Button>
          </div>
        </form>
      </div>
    </DashboardShell>
  );
}
