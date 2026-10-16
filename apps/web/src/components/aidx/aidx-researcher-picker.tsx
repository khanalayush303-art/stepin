"use client";

import { X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { AidxSearchSelect } from "./aidx-search-select";

export interface AidxResearcherPickerOption {
  id: string;
  displayName: string;
}

export interface AidxResearcherPickerValue {
  researcherId: string;
  role: string | null;
}

/** Searchable add of project team members, each with an optional free-text role, replacing a
 * read-only display-only list. */
export function AidxResearcherPicker({
  researchers,
  values,
  onChange,
  disabled,
}: {
  researchers: AidxResearcherPickerOption[];
  values: AidxResearcherPickerValue[];
  onChange: (next: AidxResearcherPickerValue[]) => void;
  disabled?: boolean;
}) {
  const nameById = new Map(researchers.map((r) => [r.id, r.displayName]));
  const options = researchers
    .filter((r) => !values.some((v) => v.researcherId === r.id))
    .map((r) => ({ id: r.id, label: r.displayName }));

  return (
    <fieldset className="space-y-3" disabled={disabled}>
      <legend className="text-small font-medium text-foreground">Team</legend>
      {values.length > 0 ? (
        <ul className="space-y-2">
          {values.map((value) => (
            <li key={value.researcherId} className="flex items-center gap-2 rounded-md border border-border p-2">
              <span className="min-w-0 flex-1 truncate text-small text-foreground">
                {nameById.get(value.researcherId) ?? "Unknown researcher"}
              </span>
              <Input
                aria-label={`Role for ${nameById.get(value.researcherId) ?? "this researcher"}`}
                placeholder="Role (optional)"
                className="h-9 w-48"
                value={value.role ?? ""}
                disabled={disabled}
                onChange={(event) =>
                  onChange(
                    values.map((v) =>
                      v.researcherId === value.researcherId ? { ...v, role: event.target.value || null } : v
                    )
                  )
                }
              />
              <Button
                type="button"
                variant="ghost"
                size="sm"
                disabled={disabled}
                aria-label={`Remove ${nameById.get(value.researcherId) ?? "researcher"}`}
                onClick={() => onChange(values.filter((v) => v.researcherId !== value.researcherId))}
              >
                <X aria-hidden="true" className="size-4" />
              </Button>
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-small text-muted-foreground">No team members linked yet.</p>
      )}
      <AidxSearchSelect
        options={options}
        placeholder="Search researchers to add…"
        disabled={disabled}
        onSelect={(option) => onChange([...values, { researcherId: option.id, role: null }])}
        emptyText="No matching researchers."
      />
    </fieldset>
  );
}
