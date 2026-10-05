"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import {
  createAdminOpportunity,
  describeAdminError,
  listPublishedProjectOptions,
  type AdminOpportunityError,
  type AidxOpportunityInput,
} from "@/lib/aidx/admin";
import type { ProjectSummary } from "@/lib/aidx/types";
import { ADMIN_NAV } from "../../../_nav";
import { OpportunityForm } from "../_components/opportunity-form";

const EMPTY: AidxOpportunityInput = {
  title: "",
  description: "",
  employmentType: "",
  workplaceType: "",
  location: "",
  compensation: null,
  skills: [],
  aidxProjectId: null,
};

export default function NewAidxOpportunityPage() {
  const router = useRouter();
  const { getToken } = useAuth();
  const [value, setValue] = React.useState<AidxOpportunityInput>(EMPTY);
  const [projects, setProjects] = React.useState<ProjectSummary[]>([]);
  const [saving, setSaving] = React.useState(false);
  const [error, setError] = React.useState<AdminOpportunityError | null>(null);

  React.useEffect(() => {
    listPublishedProjectOptions()
      .then((page) => setProjects(page.items))
      .catch(() => setProjects([]));
  }, []);

  async function onSave() {
    setSaving(true);
    setError(null);
    try {
      const token = await getToken();
      const created = await createAdminOpportunity(token, value);
      // The server creates every opportunity as a draft, so it is opened for review before any publishing.
      router.push(`/admin/aidx/opportunities/${created.id}/edit`);
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
          <Link href="/admin/aidx/opportunities" className="underline-offset-4 hover:underline">
            Research opportunities
          </Link>
          <span aria-hidden="true"> / </span>
          <span className="text-foreground">New</span>
        </nav>

        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Create research opportunity</h1>
          <p className="text-body text-muted-foreground">
            Saved as a draft. It is not visible to candidates until you publish it.
          </p>
        </div>

        {error ? (
          <Alert tone="error" title={error.kind === "validation" ? "Check the form" : "Couldn't create this opportunity"}>
            {error.message}
            {error.kind === "unauthenticated" ? (
              <>
                {" "}
                <Link href="/sign-in?returnTo=/admin/aidx/opportunities/new" className="font-semibold underline underline-offset-4">
                  Sign in
                </Link>
              </>
            ) : null}
          </Alert>
        ) : null}

        <form
          onSubmit={(event) => {
            event.preventDefault();
            void onSave();
          }}
          className="space-y-6"
        >
          <OpportunityForm
            value={value}
            onChange={setValue}
            fieldErrors={fieldErrors}
            disabled={saving}
            projects={projects}
            currentProject={null}
          />
          <div className="flex justify-end gap-3">
            <Button variant="outline" asChild>
              <Link href="/admin/aidx/opportunities">Cancel</Link>
            </Button>
            <Button type="submit" size="lg" loading={saving} loadingText="Saving…">
              Save draft
            </Button>
          </div>
        </form>
      </div>
    </DashboardShell>
  );
}
