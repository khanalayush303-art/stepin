"use client";

import * as React from "react";
import { ArrowDown, ArrowUp, Plus, X } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import type { AdminPersonSummary } from "@/lib/aidx/people-cms";

/** One author in the editor. The key is local only, so reordering keeps React state stable. */
export interface AuthorDraft {
  key: string;
  researcherId: string | null;
  externalAuthorName: string | null;
  displayName: string;
  /** Null for external authors. False when the linked researcher is currently hidden. */
  researcherPublished: boolean | null;
}

export function newAuthorKey(): string {
  return globalThis.crypto?.randomUUID?.() ?? `author-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

/**
 * Ordered author list. Order is the array order, and the backend stores it as positions 1..n. Every
 * action is a real button with a name, so the list works with the keyboard and screen readers.
 * A researcher can appear only once. Hidden researchers stay as authors, shown with a text label.
 */
export function AuthorEditor({
  authors,
  onChange,
  researchers,
  disabled,
  error,
}: {
  authors: AuthorDraft[];
  onChange: (authors: AuthorDraft[]) => void;
  researchers: AdminPersonSummary[];
  disabled?: boolean;
  error?: string;
}) {
  const [pickedResearcher, setPickedResearcher] = React.useState<string>("");
  const [externalName, setExternalName] = React.useState("");

  const linkedIds = new Set(authors.map((a) => a.researcherId).filter((id): id is string => Boolean(id)));
  const available = researchers.filter((r) => !linkedIds.has(r.id));

  function addResearcher() {
    const person = researchers.find((r) => r.id === pickedResearcher);
    if (!person) return;
    onChange([
      ...authors,
      {
        key: newAuthorKey(),
        researcherId: person.id,
        externalAuthorName: null,
        displayName: person.displayName,
        researcherPublished: person.published,
      },
    ]);
    setPickedResearcher("");
  }

  function addExternal() {
    const name = externalName.trim();
    if (!name) return;
    onChange([
      ...authors,
      { key: newAuthorKey(), researcherId: null, externalAuthorName: name, displayName: name, researcherPublished: null },
    ]);
    setExternalName("");
  }

  function move(index: number, direction: -1 | 1) {
    const target = index + direction;
    if (target < 0 || target >= authors.length) return;
    const next = [...authors];
    [next[index], next[target]] = [next[target], next[index]];
    onChange(next);
  }

  function remove(index: number) {
    onChange(authors.filter((_, i) => i !== index));
  }

  return (
    <fieldset className="space-y-4" disabled={disabled} aria-describedby={error ? "authors-error" : undefined}>
      <legend className="text-small font-medium text-foreground">Authors, in order</legend>

      {authors.length === 0 ? (
        <p className="text-small text-muted-foreground">No authors yet. Add a lab researcher or an external author below.</p>
      ) : (
        <ol className="space-y-2" aria-label="Author order">
          {authors.map((author, index) => (
            <li key={author.key} className="flex flex-wrap items-center gap-2 rounded-md border border-border p-3">
              <span className="w-8 shrink-0 text-small text-muted-foreground" aria-hidden="true">
                {index + 1}.
              </span>
              <span className="min-w-0 flex-1 break-words text-small text-foreground">
                {author.displayName}
                {author.researcherId === null ? <span className="text-muted-foreground"> (external)</span> : null}
              </span>
              {author.researcherPublished === false ? (
                <Badge tone="neutral" variant="subtle" showDot>
                  Hidden researcher
                </Badge>
              ) : null}
              <div className="flex gap-1">
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  aria-label={`Move ${author.displayName} up`}
                  onClick={() => move(index, -1)}
                  disabled={index === 0}
                >
                  <ArrowUp aria-hidden="true" className="size-4" />
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  aria-label={`Move ${author.displayName} down`}
                  onClick={() => move(index, 1)}
                  disabled={index === authors.length - 1}
                >
                  <ArrowDown aria-hidden="true" className="size-4" />
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  aria-label={`Remove ${author.displayName} from authors`}
                  onClick={() => remove(index)}
                >
                  <X aria-hidden="true" className="size-4" />
                </Button>
              </div>
            </li>
          ))}
        </ol>
      )}

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <label htmlFor="add-researcher" className="text-small font-medium text-foreground">
            Add a lab researcher
          </label>
          <div className="flex gap-2">
            <Select value={pickedResearcher || undefined} onValueChange={setPickedResearcher} disabled={available.length === 0}>
              <SelectTrigger id="add-researcher" className="min-w-0 flex-1">
                <SelectValue placeholder={available.length === 0 ? "No more researchers" : "Choose a researcher"} />
              </SelectTrigger>
              <SelectContent>
                {available.map((person) => (
                  <SelectItem key={person.id} value={person.id}>
                    {person.displayName}
                    {person.published ? "" : " (hidden)"}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button type="button" variant="outline" onClick={addResearcher} disabled={!pickedResearcher} aria-label="Add selected researcher">
              <Plus aria-hidden="true" className="size-4" />
              Add
            </Button>
          </div>
        </div>

        <div className="space-y-2">
          <label htmlFor="add-external" className="text-small font-medium text-foreground">
            Add an external author
          </label>
          <div className="flex gap-2">
            <Input
              id="add-external"
              value={externalName}
              onChange={(event) => setExternalName(event.target.value)}
              placeholder="Full name"
              className="min-w-0 flex-1"
              onKeyDown={(event) => {
                if (event.key === "Enter") {
                  event.preventDefault();
                  addExternal();
                }
              }}
            />
            <Button type="button" variant="outline" onClick={addExternal} disabled={!externalName.trim()} aria-label="Add external author">
              <Plus aria-hidden="true" className="size-4" />
              Add
            </Button>
          </div>
        </div>
      </div>

      {error ? (
        <p id="authors-error" role="alert" className="text-small text-error">
          {error}
        </p>
      ) : null}
    </fieldset>
  );
}
