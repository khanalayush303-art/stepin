"use client";

import * as React from "react";
import { ChevronDown, SlidersHorizontal, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { cn } from "@/lib/utils";

export interface FilterGroup {
  id: string;
  label: string;
  options: { value: string; label: string; count: number }[];
}

export interface JobFiltersProps {
  groups: FilterGroup[];
  selected: string[];
  onToggle: (value: string) => void;
  onClear: () => void;
}

function FilterSection({
  group,
  selected,
  onToggle,
}: {
  group: FilterGroup;
  selected: string[];
  onToggle: (v: string) => void;
}) {
  const [open, setOpen] = React.useState(true);
  const panelId = `filter-panel-${group.id}`;

  return (
    <div className="border-b border-border p-5 last:border-b-0">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        aria-controls={panelId}
        className="flex w-full items-center justify-between gap-2 rounded-sm text-small font-medium text-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      >
        {group.label}
        <ChevronDown
          className={cn("size-4 text-muted-foreground transition-transform", open && "rotate-180")}
          aria-hidden="true"
        />
      </button>

      <div id={panelId} hidden={!open} className="mt-3 flex flex-col gap-3">
        {group.options.map((option) => {
          const id = `${group.id}-${option.value}`.replace(/\s+/g, "-").toLowerCase();
          return (
            <div key={option.value} className="flex items-center gap-2.5">
              <Checkbox
                id={id}
                checked={selected.includes(option.value)}
                onCheckedChange={() => onToggle(option.value)}
              />
              <label htmlFor={id} className="flex-1 cursor-pointer text-small text-foreground">
                {option.label}
              </label>
              <span className="text-caption text-muted-foreground" aria-hidden="true">
                {option.count}
              </span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

/**
 * One panel instance only.
 *
 * Rendering the same markup twice (once for mobile, once for desktop) would
 * duplicate every checkbox id and every label association, so the panel is
 * rendered once and the disclosure is handled with CSS at the lg breakpoint.
 */
export function JobFilters({ groups, selected, onToggle, onClear }: JobFiltersProps) {
  const [mobileOpen, setMobileOpen] = React.useState(false);

  return (
    <aside aria-label="Filter roles" className="lg:w-[300px] lg:shrink-0">
      <Button
        variant="outline"
        onClick={() => setMobileOpen((v) => !v)}
        aria-expanded={mobileOpen}
        aria-controls="filters-panel"
        className="w-full lg:hidden"
      >
        <SlidersHorizontal />
        {mobileOpen ? "Hide filters" : "Show filters"}
        {selected.length > 0 ? (
          <span className="ml-1 rounded-full bg-primary px-2 py-0.5 text-caption text-primary-foreground">
            {selected.length}
          </span>
        ) : null}
      </Button>

      <div
        id="filters-panel"
        className={cn(
          "mt-3 rounded-lg border border-border bg-card lg:mt-0",
          !mobileOpen && "hidden lg:block"
        )}
      >
        <div className="flex items-center justify-between gap-3 border-b border-border p-5">
          <h2 className="text-h4 text-foreground">Filters</h2>
          <Button variant="link" size="sm" className="h-auto p-0" onClick={onClear}>
            Clear all
          </Button>
        </div>
        {groups.map((group) => (
          <FilterSection key={group.id} group={group} selected={selected} onToggle={onToggle} />
        ))}
      </div>
    </aside>
  );
}

export function ActiveFilterChips({
  selected,
  labels,
  onRemove,
}: {
  selected: string[];
  labels: Record<string, string>;
  onRemove: (value: string) => void;
}) {
  if (selected.length === 0) return null;
  return (
    <ul className="flex flex-wrap gap-2">
      {selected.map((value) => (
        <li key={value}>
          <button
            type="button"
            onClick={() => onRemove(value)}
            className="inline-flex items-center gap-2 rounded-full bg-primary-subtle px-3 py-1.5 text-caption text-primary-subtle-fg hover:bg-primary-subtle/70 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            {labels[value] ?? value}
            <X className="size-3" aria-hidden="true" />
            <span className="sr-only">Remove filter</span>
          </button>
        </li>
      ))}
    </ul>
  );
}
