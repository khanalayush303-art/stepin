"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { describeAdminError, type AdminOpportunityError } from "@/lib/aidx/admin";
import { createAdminResearchArea, type ResearchAreaInput } from "@/lib/aidx/cms";
import { ADMIN_NAV } from "../../../_nav";
import { ResearchAreaForm } from "../_components/research-area-form";

const EMPTY: ResearchAreaInput = { name: "", slug: null, description: null, sortOrder: 0 };

export default function NewResearchAreaPage() {
  const router = useRouter();
  const { getToken } = useAuth();
  const [value, setValue] = React.useState<ResearchAreaInput>(EMPTY);
  const [saving, setSaving] = React.useState(false);
  const [error, setError] = React.useState<AdminOpportunityError | null>(null);

  async function onSave() {
    setSaving(true);
    setError(null);
    try {
      const token = await getToken();
      const created = await createAdminResearchArea(token, value);
      router.push(`/admin/aidx/research/${created.id}/edit`);
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
          <Link href="/admin/aidx/research" className="underline-offset-4 hover:underline">
            Research areas
          </Link>
          <span aria-hidden="true"> / </span>
          <span className="text-foreground">New</span>
        </nav>

        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Create research area</h1>
          <p className="text-body text-muted-foreground">It appears on the public research page as soon as it is saved.</p>
        </div>

        {error ? (
          <Alert tone="error" title={error.kind === "validation" ? "Check the form" : "Couldn't create this research area"}>
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
          <ResearchAreaForm value={value} onChange={setValue} fieldErrors={fieldErrors} disabled={saving} />
          <div className="flex justify-end gap-3">
            <Button variant="outline" asChild>
              <Link href="/admin/aidx/research">Cancel</Link>
            </Button>
            <Button type="submit" size="lg" loading={saving} loadingText="Saving…">
              Create research area
            </Button>
          </div>
        </form>
      </div>
    </DashboardShell>
  );
}
