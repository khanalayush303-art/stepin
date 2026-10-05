"use client";

import { Card } from "@/components/ui/card";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import type { ResearchAreaInput } from "@/lib/aidx/cms";

/**
 * Research area fields. The slug is optional: the backend derives it from the name when it is left
 * empty, and rejects duplicates or malformed slugs with a message this form shows against the field.
 */
export function ResearchAreaForm({
  value,
  onChange,
  fieldErrors,
  disabled,
}: {
  value: ResearchAreaInput;
  onChange: (value: ResearchAreaInput) => void;
  fieldErrors: Record<string, string[]>;
  disabled?: boolean;
}) {
  return (
    <Card className="space-y-4 p-6">
      <FormField id="name" label="Name" error={fieldErrors.name?.[0]}>
        {(props) => (
          <Input
            {...props}
            value={value.name}
            onChange={(event) => onChange({ ...value, name: event.target.value })}
            placeholder="AI & Machine Learning"
            disabled={disabled}
          />
        )}
      </FormField>

      <FormField id="slug" label="URL slug" hint="Optional. Leave empty to create one from the name." error={fieldErrors.slug?.[0]}>
        {(props) => (
          <Input
            {...props}
            value={value.slug ?? ""}
            onChange={(event) => onChange({ ...value, slug: event.target.value.trim() || null })}
            placeholder="ai-and-machine-learning"
            disabled={disabled}
            autoCapitalize="none"
            spellCheck={false}
          />
        )}
      </FormField>

      <FormField id="description" label="Description" hint="Optional." error={fieldErrors.description?.[0]}>
        {(props) => (
          <Textarea
            {...props}
            value={value.description ?? ""}
            onChange={(event) => onChange({ ...value, description: event.target.value.trim() || null })}
            rows={5}
            disabled={disabled}
          />
        )}
      </FormField>

      <FormField id="sortOrder" label="Display order" hint="Lower numbers appear first." error={fieldErrors.sortOrder?.[0]}>
        {(props) => (
          <Input
            {...props}
            type="number"
            inputMode="numeric"
            min={0}
            value={String(value.sortOrder)}
            onChange={(event) => onChange({ ...value, sortOrder: Number.parseInt(event.target.value || "0", 10) || 0 })}
            disabled={disabled}
          />
        )}
      </FormField>
    </Card>
  );
}
