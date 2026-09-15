"use client";

import * as React from "react";
import { AlertCircle, SearchX } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
import { JobListSkeleton } from "@/components/ui/skeleton";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { ActiveFilterChips, JobFilters, type FilterGroup } from "./job-filters";
import { JobCard } from "./job-card";
import type { Job } from "@/lib/types";

export type BrowseState = "ready" | "loading" | "empty" | "error";

const PAGE_SIZE = 4;

export interface JobsBrowserProps {
  jobs: Job[];
  groups: FilterGroup[];
  /**
   * Forces a presentation state. Driven by ?state= on the page so every state in
   * the design can be reviewed and covered by a test without a backend.
   */
  state?: BrowseState;
  emptyCopy?: { title: string; description: string };
  countNoun?: string;
}

export function JobsBrowser({
  jobs,
  groups,
  state = "ready",
  emptyCopy,
  countNoun = "roles",
}: JobsBrowserProps) {
  const [selected, setSelected] = React.useState<string[]>([]);
  const [page, setPage] = React.useState(1);
  const [sort, setSort] = React.useState("relevant");

  const labels = React.useMemo(() => {
    const map: Record<string, string> = {};
    groups.forEach((g) => g.options.forEach((o) => (map[o.value] = o.label)));
    return map;
  }, [groups]);

  const toggle = (value: string) => {
    setPage(1);
    setSelected((prev) =>
      prev.includes(value) ? prev.filter((v) => v !== value) : [...prev, value]
    );
  };

  const filtered = React.useMemo(() => {
    if (selected.length === 0) return jobs;
    return jobs.filter((job) => {
      const facets = [job.workType, job.experience, job.location, job.workMode];
      return selected.every((value) => facets.includes(value as never));
    });
  }, [jobs, selected]);

  const sorted = React.useMemo(() => {
    const copy = [...filtered];
    if (sort === "newest") {
      copy.sort((a, b) => +new Date(b.postedAt) - +new Date(a.postedAt));
    } else if (sort === "closing") {
      copy.sort((a, b) => +new Date(a.closesAt) - +new Date(b.closesAt));
    }
    return copy;
  }, [filtered, sort]);

  const totalPages = Math.max(1, Math.ceil(sorted.length / PAGE_SIZE));
  const pageItems = sorted.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
  const showEmpty = state === "empty" || (state === "ready" && sorted.length === 0);

  return (
    <div className="container-page flex flex-col gap-8 py-10 lg:flex-row lg:py-12">
      <JobFilters
        groups={groups}
        selected={selected}
        onToggle={toggle}
        onClear={() => {
          setSelected([]);
          setPage(1);
        }}
      />

      <div className="min-w-0 flex-1 space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <p className="text-small text-muted-foreground" aria-live="polite">
            {state === "loading"
              ? `Loading ${countNoun}…`
              : state === "error"
                ? `Could not load ${countNoun}`
                : `Showing ${pageItems.length} of ${sorted.length} ${countNoun}`}
          </p>

          <div className="flex items-center gap-2">
            <label htmlFor="sort" className="sr-only">
              Sort results
            </label>
            <Select value={sort} onValueChange={setSort}>
              <SelectTrigger id="sort" className="h-control-sm w-56 text-small">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="relevant">Sort: Most relevant</SelectItem>
                <SelectItem value="newest">Sort: Newest first</SelectItem>
                <SelectItem value="closing">Sort: Closing soonest</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>

        <ActiveFilterChips selected={selected} labels={labels} onRemove={toggle} />

        {state === "loading" ? (
          <JobListSkeleton />
        ) : state === "error" ? (
          <EmptyState
            tone="error"
            icon={<AlertCircle className="size-6" />}
            title={`We could not load these ${countNoun}`}
            description="Something went wrong on our side, not yours. Your filters are still saved."
            action={<Button onClick={() => window.location.reload()}>Try again</Button>}
          />
        ) : showEmpty ? (
          <EmptyState
            icon={<SearchX className="size-6" />}
            title={emptyCopy?.title ?? `No ${countNoun} match those filters`}
            description={
              emptyCopy?.description ??
              "Try widening the location or removing a filter. There are more roles live right now."
            }
            action={
              <Button
                onClick={() => {
                  setSelected([]);
                  setPage(1);
                }}
              >
                Clear all filters
              </Button>
            }
          />
        ) : (
          <>
            <ul className="space-y-4">
              {pageItems.map((job) => (
                <li key={job.id} className="relative">
                  <JobCard job={job} />
                </li>
              ))}
            </ul>
            <Pagination
              page={page}
              totalPages={totalPages}
              onPageChange={setPage}
              className="pt-4"
            />
          </>
        )}
      </div>
    </div>
  );
}
