"use client";

import { Plus, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { AidxResearcherPicker, type AidxResearcherPickerOption } from "@/components/aidx/aidx-researcher-picker";
import { AidxTechnologyPicker } from "@/components/aidx/aidx-technology-picker";
import type { ProjectInput, ProjectMilestone } from "@/lib/aidx/cms";
import type { ResearchArea } from "@/lib/aidx/types";
import { ResearchAreaPicker } from "./research-area-picker";

function emptyMilestone(): ProjectMilestone {
  return { title: "", description: null, targetDate: null, completedDate: null };
}

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
  researchers,
  technologySuggestions,
}: {
  value: ProjectInput;
  onChange: (value: ProjectInput) => void;
  fieldErrors: Record<string, string[]>;
  disabled?: boolean;
  areas: ResearchArea[];
  researchers: AidxResearcherPickerOption[];
  technologySuggestions: string[];
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
        <FormField id="outcomes" label="Outcomes" hint="Optional. Results or impact, shown on the project's public page." error={fieldErrors.outcomes?.[0]}>
          {(props) => (
            <Textarea
              {...props}
              value={value.outcomes ?? ""}
              onChange={(event) => onChange({ ...value, outcomes: event.target.value.trim() || null })}
              rows={4}
              disabled={disabled}
            />
          )}
        </FormField>
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
        <AidxTechnologyPicker
          values={value.technologies}
          suggestions={technologySuggestions}
          onChange={(technologies) => onChange({ ...value, technologies })}
          disabled={disabled}
          error={fieldErrors.technologies?.[0]}
        />
      </Card>

      <Card className="space-y-4 p-6">
        <AidxResearcherPicker
          researchers={researchers}
          values={value.researchers}
          onChange={(researchers) => onChange({ ...value, researchers })}
          disabled={disabled}
        />
      </Card>

      <Card className="space-y-4 p-6">
        <div className="flex items-center justify-between">
          <h2 className="text-h4 text-foreground">Milestones</h2>
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={disabled}
            onClick={() => onChange({ ...value, milestones: [...value.milestones, emptyMilestone()] })}
          >
            <Plus aria-hidden="true" className="size-4" />
            Add milestone
          </Button>
        </div>
        {fieldErrors.milestones?.[0] ? <p className="text-caption text-error">{fieldErrors.milestones[0]}</p> : null}
        {value.milestones.length === 0 ? (
          <p className="text-small text-muted-foreground">No milestones yet. Shown on the project&rsquo;s public page, in order.</p>
        ) : (
          <ul className="space-y-4">
            {value.milestones.map((milestone, index) => (
              <li key={index} className="space-y-3 rounded-md border border-border p-4">
                <div className="flex items-start justify-between gap-3">
                  <span className="pt-2 text-small font-medium text-muted-foreground">#{index + 1}</span>
                  <div className="flex gap-2">
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      disabled={disabled || index === 0}
                      onClick={() => {
                        const next = [...value.milestones];
                        [next[index - 1], next[index]] = [next[index], next[index - 1]];
                        onChange({ ...value, milestones: next });
                      }}
                    >
                      Move up
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      disabled={disabled || index === value.milestones.length - 1}
                      onClick={() => {
                        const next = [...value.milestones];
                        [next[index], next[index + 1]] = [next[index + 1], next[index]];
                        onChange({ ...value, milestones: next });
                      }}
                    >
                      Move down
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      disabled={disabled}
                      aria-label={`Remove milestone ${index + 1}`}
                      onClick={() => onChange({ ...value, milestones: value.milestones.filter((_, i) => i !== index) })}
                    >
                      <Trash2 aria-hidden="true" className="size-4" />
                    </Button>
                  </div>
                </div>
                <FormField id={`milestone-${index}-title`} label="Title">
                  {(props) => (
                    <Input
                      {...props}
                      value={milestone.title}
                      disabled={disabled}
                      onChange={(event) => {
                        const next = [...value.milestones];
                        next[index] = { ...milestone, title: event.target.value };
                        onChange({ ...value, milestones: next });
                      }}
                    />
                  )}
                </FormField>
                <FormField id={`milestone-${index}-description`} label="Description" hint="Optional.">
                  {(props) => (
                    <Textarea
                      {...props}
                      value={milestone.description ?? ""}
                      rows={2}
                      disabled={disabled}
                      onChange={(event) => {
                        const next = [...value.milestones];
                        next[index] = { ...milestone, description: event.target.value.trim() || null };
                        onChange({ ...value, milestones: next });
                      }}
                    />
                  )}
                </FormField>
                <div className="grid gap-4 sm:grid-cols-2">
                  <FormField id={`milestone-${index}-target`} label="Target date" hint="Optional.">
                    {(props) => (
                      <Input
                        {...props}
                        type="date"
                        value={milestone.targetDate ?? ""}
                        disabled={disabled}
                        onChange={(event) => {
                          const next = [...value.milestones];
                          next[index] = { ...milestone, targetDate: event.target.value || null };
                          onChange({ ...value, milestones: next });
                        }}
                      />
                    )}
                  </FormField>
                  <FormField id={`milestone-${index}-completed`} label="Completed date" hint="Optional. Set once it's done.">
                    {(props) => (
                      <Input
                        {...props}
                        type="date"
                        value={milestone.completedDate ?? ""}
                        disabled={disabled}
                        onChange={(event) => {
                          const next = [...value.milestones];
                          next[index] = { ...milestone, completedDate: event.target.value || null };
                          onChange({ ...value, milestones: next });
                        }}
                      />
                    )}
                  </FormField>
                </div>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
}
