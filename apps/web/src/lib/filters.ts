import type { FilterGroup } from "@/components/jobs/job-filters";
import type { Job } from "./types";

/** Build filter facets from the current result set so counts are never stale. */
export function buildJobFilters(jobs: Job[]): FilterGroup[] {
  const count = (pick: (j: Job) => string) =>
    jobs.reduce<Record<string, number>>((acc, job) => {
      const key = pick(job);
      acc[key] = (acc[key] ?? 0) + 1;
      return acc;
    }, {});

  const toOptions = (map: Record<string, number>) =>
    Object.entries(map)
      .sort((a, b) => b[1] - a[1])
      .map(([value, n]) => ({ value, label: value, count: n }));

  return [
    { id: "work-type", label: "Work type", options: toOptions(count((j) => j.workType)) },
    { id: "experience", label: "Experience", options: toOptions(count((j) => j.experience)) },
    { id: "location", label: "Location", options: toOptions(count((j) => j.location)) },
    { id: "work-mode", label: "Work mode", options: toOptions(count((j) => j.workMode)) },
  ];
}
