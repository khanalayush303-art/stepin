"use client";

import * as React from "react";
import { BookOpen, Search } from "lucide-react";
import { Checkbox } from "@/components/ui/checkbox";
import { EmptyState } from "@/components/ui/empty-state";
import { Input } from "@/components/ui/input";
import type { ResearchArea } from "@/lib/aidx/types";

/**
 * Multi-select of research areas. Each option is a real labelled checkbox, so it works with the
 * keyboard and with screen readers, and selection is never shown by colour alone. A search box
 * filters the visible options (not a separate combobox) so toggling several areas in a row from a
 * long list stays a single click each, rather than re-searching and re-picking one at a time.
 */
export function ResearchAreaPicker({
  areas,
  selectedIds,
  onChange,
  disabled,
  error,
}: {
  areas: ResearchArea[];
  selectedIds: string[];
  onChange: (ids: string[]) => void;
  disabled?: boolean;
  error?: string;
}) {
  const [search, setSearch] = React.useState("");

  if (areas.length === 0) {
    return (
      <EmptyState
        icon={<BookOpen aria-hidden="true" className="size-6" />}
        title="No research areas yet"
        description="Create research areas first, then link them to this project."
      />
    );
  }

  function toggle(id: string, checked: boolean) {
    const next = checked ? [...selectedIds, id] : selectedIds.filter((existing) => existing !== id);
    onChange(Array.from(new Set(next)));
  }

  const term = search.trim().toLowerCase();
  const visible = term ? areas.filter((area) => area.name.toLowerCase().includes(term)) : areas;

  return (
    <fieldset className="space-y-3" aria-describedby={error ? "research-areas-error" : undefined} disabled={disabled}>
      <legend className="text-small font-medium text-foreground">Research areas</legend>
      {areas.length > 6 ? (
        <div className="relative">
          <Search aria-hidden="true" className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            className="pl-9"
            placeholder="Search research areas…"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            aria-label="Search research areas"
          />
        </div>
      ) : null}
      {visible.length === 0 ? <p className="text-small text-muted-foreground">No research areas match &ldquo;{search}&rdquo;.</p> : null}
      <ul className="grid gap-2 sm:grid-cols-2">
        {visible.map((area) => {
          const id = `research-area-${area.id}`;
          return (
            <li key={area.id} className="flex items-start gap-3 rounded-md border border-border p-3">
              <Checkbox
                id={id}
                checked={selectedIds.includes(area.id)}
                onCheckedChange={(checked) => toggle(area.id, checked === true)}
                className="mt-0.5"
              />
              <label htmlFor={id} className="min-w-0 break-words text-small text-foreground">
                {area.name}
              </label>
            </li>
          );
        })}
      </ul>
      {error ? (
        <p id="research-areas-error" role="alert" className="text-small text-error">
          {error}
        </p>
      ) : null}
    </fieldset>
  );
}
