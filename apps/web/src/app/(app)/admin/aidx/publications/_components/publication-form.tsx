"use client";

import { Card } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { labels, PUBLICATION_TYPES } from "@/lib/aidx/format";
import type { AdminPersonSummary } from "@/lib/aidx/people-cms";
import type { PublicationType, ResearchArea } from "@/lib/aidx/types";
import { ResearchAreaPicker } from "../../projects/_components/research-area-picker";
import { AuthorEditor, type AuthorDraft } from "./author-editor";

export interface PublicationFormValue {
  title: string;
  abstract: string | null;
  publicationType: PublicationType;
  venue: string | null;
  year: number;
  doi: string | null;
  externalUrl: string | null;
  published: boolean;
  authors: AuthorDraft[];
  researchAreaIds: string[];
  projectIds: string[];
}

export interface ProjectOption {
  id: string;
  title: string;
  /** Null when the option is a published project. Set for a linked project that is no longer published. */
  statusNote: string | null;
}

/**
 * Publication fields and the three relationship editors. Relationships are part of the form value, so a
 * save always sends the complete intended set and never a partial one.
 */
export function PublicationForm({
  value,
  onChange,
  fieldErrors,
  disabled,
  areas,
  researchers,
  projects,
}: {
  value: PublicationFormValue;
  onChange: (value: PublicationFormValue) => void;
  fieldErrors: Record<string, string[]>;
  disabled?: boolean;
  areas: ResearchArea[];
  researchers: AdminPersonSummary[];
  projects: ProjectOption[];
}) {
  function toggleProject(id: string, checked: boolean) {
    const next = checked ? [...value.projectIds, id] : value.projectIds.filter((p) => p !== id);
    onChange({ ...value, projectIds: Array.from(new Set(next)) });
  }

  return (
    <div className="space-y-6">
      <Card className="space-y-4 p-6">
        <h2 className="text-h4 text-foreground">Publication details</h2>
        <FormField id="title" label="Title" error={fieldErrors.title?.[0]}>
          {(props) => (
            <Input {...props} value={value.title} onChange={(event) => onChange({ ...value, title: event.target.value })} disabled={disabled} />
          )}
        </FormField>
        <FormField id="abstract" label="Abstract" hint="Optional. Plain text; line breaks are kept." error={fieldErrors.abstract?.[0]}>
          {(props) => (
            <Textarea
              {...props}
              value={value.abstract ?? ""}
              onChange={(event) => onChange({ ...value, abstract: event.target.value.trim() || null })}
              rows={6}
              disabled={disabled}
            />
          )}
        </FormField>
        <div className="grid gap-4 sm:grid-cols-3">
          <FormField id="publicationType" label="Type" error={fieldErrors.publicationType?.[0]}>
            {(props) => (
              <Select value={value.publicationType} onValueChange={(next) => onChange({ ...value, publicationType: next as PublicationType })} disabled={disabled}>
                <SelectTrigger id={props.id}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {PUBLICATION_TYPES.map((type) => (
                    <SelectItem key={type} value={type}>
                      {labels.publication(type)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </FormField>
          <FormField id="year" label="Year" error={fieldErrors.year?.[0]}>
            {(props) => (
              <Input
                {...props}
                type="number"
                inputMode="numeric"
                min={1900}
                max={2100}
                value={String(value.year)}
                onChange={(event) => onChange({ ...value, year: Number.parseInt(event.target.value || "0", 10) || 0 })}
                disabled={disabled}
              />
            )}
          </FormField>
          <FormField id="venue" label="Venue" hint="Journal, conference or publisher." error={fieldErrors.venue?.[0]}>
            {(props) => (
              <Input {...props} value={value.venue ?? ""} onChange={(event) => onChange({ ...value, venue: event.target.value.trim() || null })} disabled={disabled} />
            )}
          </FormField>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField id="doi" label="DOI" hint="Optional. Must be unique." error={fieldErrors.doi?.[0]}>
            {(props) => (
              <Input {...props} value={value.doi ?? ""} onChange={(event) => onChange({ ...value, doi: event.target.value.trim() || null })} placeholder="10.1000/example" disabled={disabled} />
            )}
          </FormField>
          <FormField id="externalUrl" label="Publication link" hint="Optional. Must start with https:// or http://." error={fieldErrors.externalUrl?.[0]}>
            {(props) => (
              <Input
                {...props}
                type="url"
                value={value.externalUrl ?? ""}
                onChange={(event) => onChange({ ...value, externalUrl: event.target.value.trim() || null })}
                placeholder="https://"
                disabled={disabled}
              />
            )}
          </FormField>
        </div>
      </Card>

      <Card className="p-6">
        <AuthorEditor
          authors={value.authors}
          onChange={(authors) => onChange({ ...value, authors })}
          researchers={researchers}
          disabled={disabled}
          error={fieldErrors.authors?.[0]}
        />
      </Card>

      <Card className="p-6">
        <ResearchAreaPicker
          areas={areas}
          selectedIds={value.researchAreaIds}
          onChange={(ids) => onChange({ ...value, researchAreaIds: ids })}
          disabled={disabled}
          error={fieldErrors.researchAreaIds?.[0]}
        />
      </Card>

      <Card className="space-y-4 p-6">
        <fieldset className="space-y-3" disabled={disabled} aria-describedby={fieldErrors.projectIds ? "projects-error" : undefined}>
          <legend className="text-small font-medium text-foreground">Related projects</legend>
          {projects.length === 0 ? (
            <p className="text-small text-muted-foreground">No published projects yet. Publish a project to link it here.</p>
          ) : (
            <ul className="grid gap-2 sm:grid-cols-2">
              {projects.map((project) => {
                const id = `project-${project.id}`;
                return (
                  <li key={project.id} className="flex items-start gap-3 rounded-md border border-border p-3">
                    <Checkbox
                      id={id}
                      checked={value.projectIds.includes(project.id)}
                      onCheckedChange={(checked) => toggleProject(project.id, checked === true)}
                      className="mt-0.5"
                    />
                    <label htmlFor={id} className="min-w-0 break-words text-small text-foreground">
                      {project.title}
                      {project.statusNote ? <span className="text-muted-foreground"> ({project.statusNote})</span> : null}
                    </label>
                  </li>
                );
              })}
            </ul>
          )}
          {fieldErrors.projectIds ? (
            <p id="projects-error" role="alert" className="text-small text-error">
              {fieldErrors.projectIds[0]}
            </p>
          ) : null}
        </fieldset>
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
              Show on the public publications page
            </label>
            <p className="text-small text-muted-foreground">
              {value.published
                ? "Visible: this publication is listed on /aidx/publications."
                : "Hidden: the publication is kept in the CMS only and is not listed publicly."}
            </p>
          </div>
        </div>
      </Card>
    </div>
  );
}
