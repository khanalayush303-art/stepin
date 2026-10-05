"use client";

import { Card } from "@/components/ui/card";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";

/** Form state. Times are held as datetime-local strings and converted only when saving. */
export interface EventFormValue {
  title: string;
  slug: string | null;
  description: string;
  startsLocal: string;
  endsLocal: string;
  location: string | null;
  registrationUrl: string | null;
  speakerName: string | null;
}

/**
 * Event fields. The slug is kept on edit unless it is deliberately changed, because the public URL is built
 * from it. Times are entered in the browser's local time and converted on save. The public site displays them
 * in Sydney time, so the admin should work in that timezone.
 */
export function EventForm({
  value,
  onChange,
  fieldErrors,
  disabled,
}: {
  value: EventFormValue;
  onChange: (value: EventFormValue) => void;
  fieldErrors: Record<string, string[]>;
  disabled?: boolean;
}) {
  return (
    <div className="space-y-6">
      <Card className="space-y-4 p-6">
        <h2 className="text-h4 text-foreground">Event</h2>
        <FormField id="title" label="Title" error={fieldErrors.title?.[0]}>
          {(props) => (
            <Input {...props} value={value.title} onChange={(event) => onChange({ ...value, title: event.target.value })} disabled={disabled} />
          )}
        </FormField>
        <FormField
          id="slug"
          label="URL slug"
          hint="Leave empty to keep the current address, or to create one from the title on a new event. Changing it breaks existing links."
          error={fieldErrors.slug?.[0]}
        >
          {(props) => (
            <Input
              {...props}
              value={value.slug ?? ""}
              onChange={(event) => onChange({ ...value, slug: event.target.value.trim() || null })}
              placeholder="aidx-research-showcase-2026"
              disabled={disabled}
              autoCapitalize="none"
              spellCheck={false}
            />
          )}
        </FormField>
        <FormField id="description" label="Description" error={fieldErrors.description?.[0]}>
          {(props) => (
            <Textarea
              {...props}
              value={value.description}
              onChange={(event) => onChange({ ...value, description: event.target.value })}
              rows={8}
              disabled={disabled}
            />
          )}
        </FormField>
      </Card>

      <Card className="space-y-4 p-6">
        <h2 className="text-h4 text-foreground">Schedule and place</h2>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField id="startsLocal" label="Starts" hint="Your local time (Sydney time recommended)." error={fieldErrors.startsAt?.[0]}>
            {(props) => (
              <Input
                {...props}
                type="datetime-local"
                value={value.startsLocal}
                onChange={(event) => onChange({ ...value, startsLocal: event.target.value })}
                disabled={disabled}
              />
            )}
          </FormField>
          <FormField id="endsLocal" label="Ends" hint="Optional. Must not be before the start." error={fieldErrors.endsAt?.[0]}>
            {(props) => (
              <Input
                {...props}
                type="datetime-local"
                value={value.endsLocal}
                onChange={(event) => onChange({ ...value, endsLocal: event.target.value })}
                disabled={disabled}
              />
            )}
          </FormField>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField id="location" label="Location" hint="Optional. Venue or online." error={fieldErrors.location?.[0]}>
            {(props) => (
              <Input {...props} value={value.location ?? ""} onChange={(event) => onChange({ ...value, location: event.target.value.trim() || null })} disabled={disabled} />
            )}
          </FormField>
          <FormField id="speakerName" label="Speaker" hint="Optional." error={fieldErrors.speakerName?.[0]}>
            {(props) => (
              <Input {...props} value={value.speakerName ?? ""} onChange={(event) => onChange({ ...value, speakerName: event.target.value.trim() || null })} disabled={disabled} />
            )}
          </FormField>
        </div>
        <FormField
          id="registrationUrl"
          label="Registration link"
          hint="Optional. Must start with https:// or http://."
          error={fieldErrors.registrationUrl?.[0]}
        >
          {(props) => (
            <Input
              {...props}
              type="url"
              value={value.registrationUrl ?? ""}
              onChange={(event) => onChange({ ...value, registrationUrl: event.target.value.trim() || null })}
              placeholder="https://"
              disabled={disabled}
            />
          )}
        </FormField>
      </Card>
    </div>
  );
}
