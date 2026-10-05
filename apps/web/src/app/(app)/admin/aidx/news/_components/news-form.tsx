"use client";

import { Card } from "@/components/ui/card";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import type { NewsInput } from "@/lib/aidx/news-cms";
import type { AdminPersonSummary } from "@/lib/aidx/people-cms";

/**
 * News fields. The slug is kept on edit unless it is deliberately changed, because the public URL is built
 * from it. The body is plain text, rendered as paragraphs on the public site, so there is no rich-text
 * editor and no markup to sanitise.
 */
export function NewsForm({
  value,
  onChange,
  fieldErrors,
  disabled,
  researchers,
}: {
  value: NewsInput;
  onChange: (value: NewsInput) => void;
  fieldErrors: Record<string, string[]>;
  disabled?: boolean;
  researchers: AdminPersonSummary[];
}) {
  return (
    <div className="space-y-6">
      <Card className="space-y-4 p-6">
        <h2 className="text-h4 text-foreground">Story</h2>
        <FormField id="title" label="Headline" error={fieldErrors.title?.[0]}>
          {(props) => (
            <Input {...props} value={value.title} onChange={(event) => onChange({ ...value, title: event.target.value })} disabled={disabled} />
          )}
        </FormField>
        <FormField
          id="slug"
          label="URL slug"
          hint="Leave empty to keep the current address, or to create one from the headline on a new story. Changing it breaks existing links."
          error={fieldErrors.slug?.[0]}
        >
          {(props) => (
            <Input
              {...props}
              value={value.slug ?? ""}
              onChange={(event) => onChange({ ...value, slug: event.target.value.trim() || null })}
              placeholder="ai-research-update"
              disabled={disabled}
              autoCapitalize="none"
              spellCheck={false}
            />
          )}
        </FormField>
        <FormField id="summary" label="Summary" hint="Shown in the news list. Keep it to a sentence or two." error={fieldErrors.summary?.[0]}>
          {(props) => (
            <Textarea {...props} value={value.summary} onChange={(event) => onChange({ ...value, summary: event.target.value })} rows={3} disabled={disabled} />
          )}
        </FormField>
        <FormField id="body" label="Body" hint="Plain text. Separate paragraphs with a blank line." error={fieldErrors.body?.[0]}>
          {(props) => (
            <Textarea {...props} value={value.body} onChange={(event) => onChange({ ...value, body: event.target.value })} rows={12} disabled={disabled} />
          )}
        </FormField>
      </Card>

      <Card className="p-6">
        <FormField id="author" label="Author" hint="Optional. Chosen from the lab's researchers. Researchers do not sign in." error={fieldErrors.authorResearcherId?.[0]}>
          {(props) => (
            <Select
              value={value.authorResearcherId ?? "none"}
              onValueChange={(next) => onChange({ ...value, authorResearcherId: next === "none" ? null : next })}
              disabled={disabled}
            >
              <SelectTrigger id={props.id}>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No author</SelectItem>
                {researchers.map((person) => (
                  <SelectItem key={person.id} value={person.id}>
                    {person.displayName}
                    {person.published ? "" : " (hidden)"}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
        </FormField>
      </Card>
    </div>
  );
}
