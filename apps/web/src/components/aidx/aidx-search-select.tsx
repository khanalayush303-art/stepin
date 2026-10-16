"use client";

import * as React from "react";
import { Search } from "lucide-react";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";

export interface AidxSearchSelectOption {
  id: string;
  label: string;
  /** Shown faded after the label — e.g. a role or category hint. */
  hint?: string;
}

/**
 * A small, from-scratch type-ahead combobox (no Radix Combobox/Command primitive exists in this
 * repo yet). Filters a given option list client-side as the admin types and lets them pick one
 * with the mouse or the keyboard (Up/Down/Enter/Escape) — the standard ARIA combobox/listbox
 * pattern. Used for researcher/project/research-area pickers so an admin with a long list can
 * search instead of scanning a checkbox grid or a plain <select>.
 */
export function AidxSearchSelect({
  options,
  placeholder,
  disabled,
  onSelect,
  emptyText = "No matches.",
  onCreate,
  onQueryChange,
}: {
  options: AidxSearchSelectOption[];
  placeholder: string;
  disabled?: boolean;
  onSelect: (option: AidxSearchSelectOption) => void;
  emptyText?: string;
  /** When set, a query that matches nothing offers "Add "<query>"" — for free-text values
   * (e.g. technologies) where the suggestion list is autocomplete, not a closed set. */
  onCreate?: (query: string) => void;
  /** Fired on every keystroke — for callers that fetch their own server-side matches (e.g. a user
   * search) instead of filtering a fixed, already-loaded `options` list. */
  onQueryChange?: (query: string) => void;
}) {
  const [query, setQuery] = React.useState("");
  const [open, setOpen] = React.useState(false);
  const [highlighted, setHighlighted] = React.useState(0);
  const containerRef = React.useRef<HTMLDivElement>(null);
  const listboxId = React.useId();

  const matches = React.useMemo(() => {
    const term = query.trim().toLowerCase();
    const filtered = term ? options.filter((option) => option.label.toLowerCase().includes(term)) : options;
    return filtered.slice(0, 20);
  }, [options, query]);

  const trimmedQuery = query.trim();
  const canCreate = Boolean(onCreate && trimmedQuery && !options.some((o) => o.label.toLowerCase() === trimmedQuery.toLowerCase()));

  React.useEffect(() => {
    function onOutsideClick(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", onOutsideClick);
    return () => document.removeEventListener("mousedown", onOutsideClick);
  }, []);

  function choose(option: AidxSearchSelectOption) {
    onSelect(option);
    setQuery("");
    setOpen(false);
    setHighlighted(0);
  }

  function create() {
    if (!onCreate || !trimmedQuery) return;
    onCreate(trimmedQuery);
    setQuery("");
    setOpen(false);
    setHighlighted(0);
  }

  function onKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (!open && (event.key === "ArrowDown" || event.key === "ArrowUp")) {
      setOpen(true);
      return;
    }
    if (!open) return;

    const rowCount = matches.length + (canCreate ? 1 : 0);
    if (event.key === "ArrowDown") {
      event.preventDefault();
      setHighlighted((index) => Math.min(index + 1, rowCount - 1));
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      setHighlighted((index) => Math.max(index - 1, 0));
    } else if (event.key === "Enter") {
      event.preventDefault();
      if (highlighted < matches.length) {
        const option = matches[highlighted];
        if (option) choose(option);
      } else if (canCreate) {
        create();
      }
    } else if (event.key === "Escape") {
      setOpen(false);
    }
  }

  return (
    <div ref={containerRef} className="relative">
      <div className="relative">
        <Search aria-hidden="true" className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          role="combobox"
          aria-expanded={open}
          aria-controls={listboxId}
          aria-autocomplete="list"
          className="pl-9"
          value={query}
          placeholder={placeholder}
          disabled={disabled}
          onChange={(event) => {
            setQuery(event.target.value);
            setOpen(true);
            setHighlighted(0);
            onQueryChange?.(event.target.value);
          }}
          onFocus={() => setOpen(true)}
          onKeyDown={onKeyDown}
        />
      </div>

      {open && !disabled ? (
        <ul
          id={listboxId}
          role="listbox"
          className="absolute z-20 mt-1 max-h-60 w-full overflow-auto rounded-md border border-border bg-surface py-1 shadow-md"
        >
          {matches.length === 0 && !canCreate ? (
            <li className="px-3 py-2 text-small text-muted-foreground">{emptyText}</li>
          ) : (
            matches.map((option, index) => (
              <li key={option.id} role="option" aria-selected={index === highlighted}>
                <button
                  type="button"
                  className={cn(
                    "flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-small",
                    index === highlighted ? "bg-muted" : "hover:bg-muted"
                  )}
                  onMouseEnter={() => setHighlighted(index)}
                  onClick={() => choose(option)}
                >
                  <span className="text-foreground">{option.label}</span>
                  {option.hint ? <span className="text-caption text-muted-foreground">{option.hint}</span> : null}
                </button>
              </li>
            ))
          )}
          {canCreate ? (
            <li role="option" aria-selected={highlighted === matches.length}>
              <button
                type="button"
                className={cn(
                  "flex w-full items-center gap-2 px-3 py-2 text-left text-small font-medium text-primary",
                  highlighted === matches.length ? "bg-muted" : "hover:bg-muted"
                )}
                onMouseEnter={() => setHighlighted(matches.length)}
                onClick={create}
              >
                Add &ldquo;{trimmedQuery}&rdquo;
              </button>
            </li>
          ) : null}
        </ul>
      ) : null}
    </div>
  );
}
