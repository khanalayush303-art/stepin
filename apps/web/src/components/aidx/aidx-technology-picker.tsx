"use client";

import * as React from "react";
import { X } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { AidxSearchSelect } from "./aidx-search-select";

/**
 * Chip list + searchable add for a project's technology tags. Technologies are free text (no
 * fixed id list), so typing a name not already suggested and pressing Enter adds it as a new
 * value — the suggestion list (fetched from /api/v1/aidx/technologies) is autocomplete only,
 * never a closed set.
 */
export function AidxTechnologyPicker({
  values,
  suggestions,
  onChange,
  disabled,
  error,
}: {
  values: string[];
  suggestions: string[];
  onChange: (next: string[]) => void;
  disabled?: boolean;
  error?: string;
}) {
  function add(name: string) {
    const trimmed = name.trim();
    if (!trimmed) return;
    if (values.some((v) => v.toLowerCase() === trimmed.toLowerCase())) return;
    onChange([...values, trimmed]);
  }

  const options = suggestions
    .filter((name) => !values.some((v) => v.toLowerCase() === name.toLowerCase()))
    .map((name) => ({ id: name, label: name }));

  return (
    <fieldset className="space-y-3" disabled={disabled}>
      <legend className="text-small font-medium text-foreground">Technologies</legend>
      {values.length > 0 ? (
        <ul className="flex flex-wrap gap-2">
          {values.map((name) => (
            <li key={name}>
              <Badge tone="neutral" variant="subtle" className="gap-1.5 pr-1.5">
                {name}
                <button
                  type="button"
                  onClick={() => onChange(values.filter((v) => v !== name))}
                  aria-label={`Remove ${name}`}
                  className="rounded-full p-0.5 hover:bg-muted"
                >
                  <X aria-hidden="true" className="size-3" />
                </button>
              </Badge>
            </li>
          ))}
        </ul>
      ) : null}
      <AidxSearchSelect
        options={options}
        placeholder="Search or type a new technology…"
        disabled={disabled}
        onSelect={(option) => add(option.label)}
        onCreate={add}
        emptyText="Start typing to add a technology…"
      />
      {error ? (
        <p role="alert" className="text-small text-error">
          {error}
        </p>
      ) : null}
    </fieldset>
  );
}
