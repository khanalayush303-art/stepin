"use client";

import { Card } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import type { ProjectInput } from "@/lib/aidx/cms";
import type { ResearchArea } from "@/lib/aidx/types";
import { ResearchAreaPicker } from "./research-area-picker";

/**
 * Project fields. The slug is optional, and the backend derives it from the title when it is left
 * empty. Researcher links are shown read-only: they are preserved on save, and editing them is a
 * later phase.
 */
export function ProjectForm({
  value,
  onChange,
  fieldErrors,
  disabled,
  areas,
  researcherNames,
}: {
  value: ProjectInput;
  onChange: (value: ProjectInput) => void;
  fieldErrors: Record<string, string[]>;
  disabled?: boolean;
  areas: ResearchArea[];
  researcherNames: string[];
}) {
  return (
    <div className="space-y-6">
      <Card className="space-y-4 p-6">
        <h2 className="text-h4 text-foreground">Project details</h2>
        <FormField id="title" label="Title" error={fieldErrors.title?.[0]}>
          {(props) => (
            <Input {...props} value={value.title} onChange={(event) => onChange({ ...value, title: event.target.value })} disabled={disabled} />
          )}
        </FormField>
        <FormField id="slug" label="URL slug" hint="Optional. Leave empty to create one from the title." error={fieldErrors.slug?.[0]}>
          {(props) => (
            <Input
              {...props}
              value={value.slug ?? ""}
              onChange={(event) => onChange({ ...value, slug: event.target.value.trim() || null })}
              placeholder="coral-reef-monitoring"
              disabled={disabled}
              autoCapitalize="none"
              spellCheck={false}
            />
          )}
        </FormField>
        <FormField id="shortDescription" label="Summary" hint="Shown in project listings." error={fieldErrors.shortDescription?.[0]}>
          {(props) => (
            <Textarea
              {...props}
              value={value.shortDescription}
              onChange={(event) => onChange({ ...value, shortDescription: event.target.value })}
              rows={3}
              disabled={disabled}
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
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField id="startDate" label="Start date" hint="Optional." error={fieldErrors.startDate?.[0]}>
            {(props) => (
              <Input
                {...props}
                type="date"
                value={value.startDate ?? ""}
                onChange={(event) => onChange({ ...value, startDate: event.target.value || null })}
                disabled={disabled}
              />
            )}
          </FormField>
          <FormField id="endDate" label="End date" hint="Optional." error={fieldErrors.endDate?.[0]}>
            {(props) => (
              <Input
                {...props}
                type="date"
                value={value.endDate ?? ""}
                onChange={(event) => onChange({ ...value, endDate: event.target.value || null })}
                disabled={disabled}
              />
            )}
          </FormField>
        </div>
        <FormField id="externalUrl" label="External project page" hint="Optional. Must start with http:// or https://." error={fieldErrors.externalUrl?.[0]}>
          {(props) => (
            <Input
              {...props}
              type="url"
              value={value.externalUrl ?? ""}
              onChange={(event) => onChange({ ...value, externalUrl: event.target.value.trim() || null })}
              placeholder="https://example.org/project"
              disabled={disabled}
            />
          )}
        </FormField>
        <div className="flex items-start gap-3">
          <Checkbox
            id="featured"
            checked={value.featured}
            onCheckedChange={(checked) => onChange({ ...value, featured: checked === true })}
            disabled={disabled}
            className="mt-0.5"
          />
          <label htmlFor="featured" className="text-small text-foreground">
            Feature this project on the AIDX landing page
          </label>
        </div>
      </Card>

      <Card className="space-y-4 p-6">
        <ResearchAreaPicker
          areas={areas}
          selectedIds={value.researchAreaIds}
          onChange={(ids) => onChange({ ...value, researchAreaIds: ids })}
          disabled={disabled}
          error={fieldErrors.researchAreaIds?.[0]}
        />
      </Card>

      <Card className="space-y-4 p-6">
        <h2 className="text-h4 text-foreground">Technologies</h2>
        <FormField id="technologies" label="Technologies" hint="Separate with commas, for example: Python, PyTorch, SQL." error={fieldErrors.technologies?.[0]}>
          {(props) => (
            <Input
              {...props}
              value={value.technologies.join(", ")}
              onChange={(event) =>
                onChange({
                  ...value,
                  technologies: event.target.value.split(",").map((t) => t.trim()).filter((t) => t.length > 0),
                })
              }
              disabled={disabled}
            />
          )}
        </FormField>
      </Card>

      <Card className="space-y-3 p-6">
        <h2 className="text-h4 text-foreground">Team</h2>
        {researcherNames.length > 0 ? (
          <p className="text-small text-foreground-secondary">{researcherNames.join(", ")}</p>
        ) : (
          <p className="text-small text-muted-foreground">No researchers are linked to this project.</p>
        )}
        <p className="text-caption text-muted-foreground">
          Team links are kept when you save. Adding or removing team members is part of the researcher CMS, which comes
          in a later phase.
        </p>
      </Card>
    </div>
  );
}
