"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { EventForm, type EventFormValue } from "@/app/(app)/admin/aidx/events/_components/event-form";
import { emptyEvent, toInput } from "@/app/(app)/admin/aidx/events/_components/mapping";
import { describeAdminError, type AdminOpportunityError } from "@/lib/aidx/admin";
import { createAuthoringEvent } from "@/lib/aidx/authoring";

export default function NewWorkspaceEventPage() {
  const router = useRouter();
  const { getToken } = useAuth();
  const [value, setValue] = React.useState<EventFormValue>(emptyEvent());
  const [saving, setSaving] = React.useState(false);
  const [error, setError] = React.useState<AdminOpportunityError | null>(null);
  const [localErrors, setLocalErrors] = React.useState<Record<string, string[]>>({});

  async function onSave() {
    const prepared = toInput(value, null);
    setLocalErrors(prepared.errors);
    if (Object.keys(prepared.errors).length > 0) return;

    setSaving(true);
    setError(null);
    try {
      const token = await getToken();
      const created = await createAuthoringEvent(token, prepared.input);
      router.push(`/aidx/workspace/events/${created.id}/edit`);
    } catch (caught) {
      setError(describeAdminError(caught));
      setSaving(false);
    }
  }

  const fieldErrors = { ...(error?.kind === "validation" ? error.fields : {}), ...localErrors };

  return (
    <div className="container-page max-w-3xl space-y-6 py-10">
      <nav aria-label="Breadcrumb" className="text-small text-muted-foreground">
        <Link href="/aidx/workspace" className="underline-offset-4 hover:underline">
          My AIDX workspace
        </Link>
        <span aria-hidden="true"> / </span>
        <span className="text-foreground">New event</span>
      </nav>

      <div className="space-y-1">
        <h1 className="text-h2 text-foreground">Add an event</h1>
        <p className="text-body text-muted-foreground">Saved as your own draft until you submit it for review.</p>
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
