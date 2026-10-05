"use client";

import { Card } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import type { PersonInput } from "@/lib/aidx/people-cms";
import { labels, RESEARCHER_CATEGORIES } from "@/lib/aidx/format";
import type { ResearcherCategory } from "@/lib/aidx/types";

/**
 * Researcher fields. The public-page toggle is the only lifecycle here: a person is either shown on
 * the public people pages or hidden from them. There are no account, owner or storage fields.
 */
export function PersonForm({
  value,
  onChange,
  fieldErrors,
  disabled,
}: {
  value: PersonInput;
  onChange: (value: PersonInput) => void;
  fieldErrors: Record<string, string[]>;
  disabled?: boolean;
}) {
  return (
    <div className="space-y-6">
      <Card className="space-y-4 p-6">
        <h2 className="text-h4 text-foreground">Profile</h2>
        <FormField id="displayName" label="Full name" error={fieldErrors.displayName?.[0]}>
          {(props) => (
            <Input {...props} value={value.displayName} onChange={(event) => onChange({ ...value, displayName: event.target.value })} disabled={disabled} />
          )}
        </FormField>
        <FormField id="slug" label="URL slug" hint="Optional. The public address is /aidx/people/slug. Changing the name does not change it." error={fieldErrors.slug?.[0]}>
          {(props) => (
            <Input
              {...props}
              value={value.slug ?? ""}
              onChange={(event) => onChange({ ...value, slug: event.target.value.trim() || null })}
              placeholder="jane-citizen"
              disabled={disabled}
              autoCapitalize="none"
              spellCheck={false}
            />
          )}
        </FormField>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField id="category" label="Role" error={fieldErrors.category?.[0]}>
            {(props) => (
              <Select value={value.category} onValueChange={(next) => onChange({ ...value, category: next as ResearcherCategory })} disabled={disabled}>
                <SelectTrigger id={props.id}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {RESEARCHER_CATEGORIES.map((category) => (
                    <SelectItem key={category} value={category}>
                      {labels.researcher(category)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </FormField>
          <FormField id="position" label="Position or title" hint="Optional." error={fieldErrors.position?.[0]}>
            {(props) => (
              <Input
                {...props}
                value={value.position ?? ""}
                onChange={(event) => onChange({ ...value, position: event.target.value.trim() || null })}
                placeholder="Senior lecturer"
                disabled={disabled}
              />
            )}
          </FormField>
        </div>
        <FormField id="biography" label="Biography" hint="Optional. Plain text; line breaks are kept." error={fieldErrors.biography?.[0]}>
          {(props) => (
            <Textarea
              {...props}
              value={value.biography ?? ""}
              onChange={(event) => onChange({ ...value, biography: event.target.value.trim() || null })}
              rows={6}
              disabled={disabled}
            />
          )}
        </FormField>
      </Card>

      <Card className="space-y-4 p-6">
        <h2 className="text-h4 text-foreground">Public links</h2>
        <p className="text-small text-muted-foreground">Optional. Each must start with https:// or http://.</p>
        <div className="grid gap-4 sm:grid-cols-2">
          <LinkField id="websiteUrl" label="Website" value={value.websiteUrl} error={fieldErrors.websiteUrl?.[0]} disabled={disabled} onChange={(next) => onChange({ ...value, websiteUrl: next })} />
          <LinkField id="linkedInUrl" label="LinkedIn" value={value.linkedInUrl} error={fieldErrors.linkedInUrl?.[0]} disabled={disabled} onChange={(next) => onChange({ ...value, linkedInUrl: next })} />
          <LinkField id="orcidUrl" label="ORCID" value={value.orcidUrl} error={fieldErrors.orcidUrl?.[0]} disabled={disabled} onChange={(next) => onChange({ ...value, orcidUrl: next })} />
          <LinkField id="googleScholarUrl" label="Google Scholar" value={value.googleScholarUrl} error={fieldErrors.googleScholarUrl?.[0]} disabled={disabled} onChange={(next) => onChange({ ...value, googleScholarUrl: next })} />
        </div>
      </Card>

      <Card className="p-6">
        <div className="flex items-start gap-3">
          <Checkbox
            id="published"
            checked={value.published}
            onCheckedChange={(checked) => onChange({ ...value, published: checked === true })}
            disabled={disabled}
            className="mt-0.5"
          />
          <div className="space-y-1">
            <label htmlFor="published" className="text-small font-medium text-foreground">
              Show on the public people pages
            </label>
            <p className="text-small text-muted-foreground">
              {value.published
                ? "Visible: this person is listed on /aidx/people and has a public profile page."
                : "Hidden: this person is kept in the CMS only. Their public page returns not found."}
            </p>
          </div>
        </div>
      </Card>
    </div>
  );
}

function LinkField({
  id,
  label,
  value,
  error,
  disabled,
  onChange,
}: {
  id: string;
  label: string;
  value: string | null;
  error?: string;
  disabled?: boolean;
  onChange: (next: string | null) => void;
}) {
  return (
    <FormField id={id} label={label} error={error}>
      {(props) => (
        <Input
          {...props}
          type="url"
          value={value ?? ""}
          onChange={(event) => onChange(event.target.value.trim() || null)}
          placeholder="https://"
          disabled={disabled}
        />
      )}
    </FormField>
  );
}
