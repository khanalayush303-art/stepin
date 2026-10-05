"use client";

import { Card } from "@/components/ui/card";
import { FormField } from "@/components/ui/form-field";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import type { AidxOpportunityInput } from "@/lib/aidx/admin";
import type { ProjectSummary } from "@/lib/aidx/types";
import { JobForm } from "@/app/(app)/recruiter/jobs/_components/job-form";

/**
 * Create and edit form. The content fields reuse the StepIn job form, so validation and field
 * labels match the rest of the product. Only the optional AIDX project is added here.
 * There are deliberately no owner, company, category or status controls.
 */
export function OpportunityForm({
  value,
  onChange,
  fieldErrors,
  disabled,
  projects,
  currentProject,
}: {
  value: AidxOpportunityInput;
  onChange: (value: AidxOpportunityInput) => void;
  fieldErrors: Record<string, string[]>;
  disabled?: boolean;
  projects: ProjectSummary[];
  /** The project already linked to this opportunity. Shown even when it is not currently selectable. */
  currentProject: { id: string; title: string } | null;
}) {
  const options = currentProject && !projects.some((p) => p.id === currentProject.id)
    ? [{ id: currentProject.id, title: currentProject.title }, ...projects]
    : projects;

  return (
    <div className="space-y-6">
      <JobForm
        value={value}
        onChange={(next) => onChange({ ...next, aidxProjectId: value.aidxProjectId })}
        fieldErrors={fieldErrors}
        disabled={disabled}
      />

      <Card className="space-y-4 p-6">
        <h2 className="text-h4 text-foreground">AIDX project</h2>
        <p className="text-small text-muted-foreground">
          Optional. A published opportunity can only link to a published project. Choose &ldquo;No project&rdquo; to
          leave it unlinked.
        </p>
        <FormField id="aidxProjectId" label="Linked project" error={fieldErrors.aidxProjectId?.[0]}>
          {(props) => (
            <Select
              value={value.aidxProjectId ?? "none"}
              onValueChange={(next) => onChange({ ...value, aidxProjectId: next === "none" ? null : next })}
              disabled={disabled}
            >
              <SelectTrigger id={props.id}>
                <SelectValue placeholder="Choose a project" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No project</SelectItem>
                {options.map((project) => (
                  <SelectItem key={project.id} value={project.id}>
                    {project.title}
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
