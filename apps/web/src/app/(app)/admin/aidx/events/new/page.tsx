"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { describeAdminError, type AdminOpportunityError } from "@/lib/aidx/admin";
import { createAdminEvent } from "@/lib/aidx/events-cms";
import { ADMIN_NAV } from "../../../_nav";
import { EventForm, type EventFormValue } from "../_components/event-form";
import { emptyEvent, toInput } from "../_components/mapping";

export default function NewAidxEventPage() {
  const router = useRouter();
  const { getToken } = useAuth();
  const [value, setValue] = React.useState<EventFormValue>(emptyEvent);
  const [saving, setSaving] = React.useState(false);
  const [error, setError] = React.useState<AdminOpportunityError | null>(null);
  const [localErrors, setLocalErrors] = React.useState<Record<string, string[]>>({});

  async function onSave() {
    const prepared = toInput(value);
    setLocalErrors(prepared.errors);
    if (Object.keys(prepared.errors).length > 0) return;

    setSaving(true);
    setError(null);
    try {
      const token = await getToken();
      const created = await createAdminEvent(token, prepared.input);
      router.push(`/admin/aidx/events/${created.id}/edit`);
    } catch (caught) {
      // Values stay in the form, so a validation error does not cost the admin their work.
      setError(describeAdminError(caught));
      setSaving(false);
    }
  }

  const fieldErrors = { ...(error?.kind === "validation" ? error.fields : {}), ...localErrors };

  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="max-w-3xl space-y-6">
        <nav aria-label="Breadcrumb" className="text-small text-muted-foreground">
          <Link href="/admin/aidx/events" className="underline-offset-4 hover:underline">
            Events
          </Link>
          <span aria-hidden="true"> / </span>
          <span className="text-foreground">New</span>
        </nav>

        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Add event</h1>
          <p className="text-body text-muted-foreground">Saved as a draft. It is not public until you publish it.</p>
        </div>

        {error ? (
          <Alert tone="error" title={error.kind === "validation" ? "Check the form" : "Couldn't save this event"}>
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
          <EventForm value={value} onChange={setValue} fieldErrors={fieldErrors} disabled={saving} />
          <div className="flex justify-end gap-3">
            <Button variant="outline" asChild>
              <Link href="/admin/aidx/events">Cancel</Link>
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
