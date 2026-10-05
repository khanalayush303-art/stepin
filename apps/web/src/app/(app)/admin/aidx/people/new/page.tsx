"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { describeAdminError, type AdminOpportunityError } from "@/lib/aidx/admin";
import { createAdminPerson, type PersonInput } from "@/lib/aidx/people-cms";
import { ADMIN_NAV } from "../../../_nav";
import { PersonForm } from "../_components/person-form";

const EMPTY: PersonInput = {
  displayName: "",
  slug: null,
  category: "Academic",
  position: null,
  biography: null,
  orcidUrl: null,
  googleScholarUrl: null,
  linkedInUrl: null,
  websiteUrl: null,
  published: false,
};

export default function NewAidxPersonPage() {
  const router = useRouter();
  const { getToken } = useAuth();
  const [value, setValue] = React.useState<PersonInput>(EMPTY);
  const [saving, setSaving] = React.useState(false);
  const [error, setError] = React.useState<AdminOpportunityError | null>(null);

  async function onSave() {
    setSaving(true);
    setError(null);
    try {
      const token = await getToken();
      const created = await createAdminPerson(token, value);
      router.push(`/admin/aidx/people/${created.id}/edit`);
    } catch (caught) {
      // Field values stay in the form, so a validation error never costs the admin their typing.
      setError(describeAdminError(caught));
      setSaving(false);
    }
  }

  const fieldErrors = error?.kind === "validation" ? error.fields : {};

  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="max-w-3xl space-y-6">
        <nav aria-label="Breadcrumb" className="text-small text-muted-foreground">
          <Link href="/admin/aidx/people" className="underline-offset-4 hover:underline">
            Researchers and people
          </Link>
          <span aria-hidden="true"> / </span>
          <span className="text-foreground">New</span>
        </nav>

        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Add researcher</h1>
          <p className="text-body text-muted-foreground">New people start hidden. Turn on &ldquo;Show on the public people pages&rdquo; when they are ready.</p>
        </div>

        {error ? (
          <Alert tone="error" title={error.kind === "validation" ? "Check the form" : "Couldn't add this researcher"}>
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
          <PersonForm value={value} onChange={setValue} fieldErrors={fieldErrors} disabled={saving} />
          <div className="flex justify-end gap-3">
            <Button variant="outline" asChild>
              <Link href="/admin/aidx/people">Cancel</Link>
            </Button>
            <Button type="submit" size="lg" loading={saving} loadingText="Saving…">
              Add researcher
            </Button>
          </div>
        </form>
      </div>
    </DashboardShell>
  );
}
