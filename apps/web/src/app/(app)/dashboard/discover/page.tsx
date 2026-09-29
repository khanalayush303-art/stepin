"use client";

import * as React from "react";
import Link from "next/link";
import {
  Bell,
  Bookmark,
  Briefcase,
  Building2,
  FileText,
  GraduationCap,
  LayoutDashboard,
  MapPin,
  Search,
  User,
} from "lucide-react";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { DashboardShell, type DashboardNavItem } from "@/components/dashboard/dashboard-shell";
import { formError } from "@/lib/auth/client";
import { listPublicJobs } from "@/lib/jobs/api";
import type { EmploymentType, JobFilters, PublicJobSummary, WorkplaceType } from "@/lib/jobs/types";
import { formatDate } from "@/lib/utils";

const NAV: DashboardNavItem[] = [
  { href: "/dashboard", label: "Overview", icon: LayoutDashboard },
  { href: "/dashboard/discover", label: "Discover roles", icon: Search },
  { href: "/dashboard/applications", label: "Applications", icon: FileText, badge: "12" },
  { href: "/dashboard/saved", label: "Saved roles", icon: Bookmark, badge: "8" },
  { href: "/dashboard/interviews", label: "Interviews", icon: User, badge: "2" },
  { href: "/dashboard/prepare", label: "Prepare", icon: GraduationCap },
  { href: "/dashboard/notifications", label: "Notifications", icon: Bell, badge: "3" },
];

const EMPLOYMENT_TYPE_OPTIONS: { value: EmploymentType; label: string }[] = [
  { value: "FullTime", label: "Full-time" },
  { value: "PartTime", label: "Part-time" },
  { value: "Contract", label: "Contract" },
  { value: "Internship", label: "Internship" },
  { value: "Casual", label: "Casual" },
];

const WORKPLACE_TYPE_OPTIONS: { value: WorkplaceType; label: string }[] = [
  { value: "OnSite", label: "On-site" },
  { value: "Hybrid", label: "Hybrid" },
  { value: "Remote", label: "Remote" },
];

/** Radix Select can't carry an empty-string item value, so "clear this filter" gets its own sentinel. */
const ANY = "any";

const EMPTY_FILTERS: JobFilters = {};

export default function DiscoverRolesPage() {
  const [appliedFilters, setAppliedFilters] = React.useState<JobFilters>(EMPTY_FILTERS);
  const [searchDraft, setSearchDraft] = React.useState("");
  const [locationDraft, setLocationDraft] = React.useState("");
  const [employmentDraft, setEmploymentDraft] = React.useState<EmploymentType | typeof ANY>(ANY);
  const [workplaceDraft, setWorkplaceDraft] = React.useState<WorkplaceType | typeof ANY>(ANY);

  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [jobs, setJobs] = React.useState<PublicJobSummary[]>([]);

  const load = React.useCallback(async (filters: JobFilters) => {
    setLoading(true);
    setLoadError(undefined);
    try {
      setJobs(await listPublicJobs(filters));
    } catch (error) {
      setLoadError(formError(error));
    } finally {
      setLoading(false);
    }
  }, []);

  React.useEffect(() => {
    // Fetch-on-mount, and again whenever appliedFilters changes (Search
    // button/Enter, not live-as-you-type): same documented pattern as the
    // other Phase 2.1/2.2 list pages. `load` is a stable useCallback
    // reference, so including it here doesn't add extra reruns.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load(appliedFilters);
  }, [load, appliedFilters]);

  function onSearch(e: React.FormEvent) {
    e.preventDefault();
    setAppliedFilters({
      search: searchDraft.trim() || undefined,
      location: locationDraft.trim() || undefined,
      employmentType: employmentDraft === ANY ? undefined : employmentDraft,
      workplaceType: workplaceDraft === ANY ? undefined : workplaceDraft,
    });
  }

  function onClear() {
    setSearchDraft("");
    setLocationDraft("");
    setEmploymentDraft(ANY);
    setWorkplaceDraft(ANY);
    setAppliedFilters(EMPTY_FILTERS);
  }

  const hasActiveFilters = Object.keys(appliedFilters).length > 0;

  return (
    <DashboardShell nav={NAV} navLabel="Applicant dashboard">
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Discover roles</h1>
          <p className="text-body text-muted-foreground">Browse published jobs and internships.</p>
        </div>

        <Card className="p-4">
          <form onSubmit={onSearch} className="flex flex-wrap items-end gap-3">
            <div className="min-w-[220px] flex-1">
              <label htmlFor="search" className="sr-only">
                Search
              </label>
              <Input
                id="search"
                value={searchDraft}
                onChange={(e) => setSearchDraft(e.target.value)}
                placeholder="Search job titles and descriptions"
              />
            </div>
            <div className="min-w-[180px] flex-1">
              <label htmlFor="location" className="sr-only">
                Location
              </label>
              <Input
                id="location"
                value={locationDraft}
                onChange={(e) => setLocationDraft(e.target.value)}
                placeholder="Location"
              />
            </div>
            <Select value={employmentDraft} onValueChange={(v) => setEmploymentDraft(v as EmploymentType | typeof ANY)}>
              <SelectTrigger className="w-[160px]">
                <SelectValue placeholder="Employment type" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ANY}>Any employment type</SelectItem>
                {EMPLOYMENT_TYPE_OPTIONS.map((option) => (
                  <SelectItem key={option.value} value={option.value}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select value={workplaceDraft} onValueChange={(v) => setWorkplaceDraft(v as WorkplaceType | typeof ANY)}>
              <SelectTrigger className="w-[150px]">
                <SelectValue placeholder="Workplace type" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ANY}>Any workplace type</SelectItem>
                {WORKPLACE_TYPE_OPTIONS.map((option) => (
                  <SelectItem key={option.value} value={option.value}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button type="submit">
              <Search />
              Search
            </Button>
            {hasActiveFilters ? (
              <Button type="button" variant="ghost" onClick={onClear}>
                Clear
              </Button>
            ) : null}
          </form>
        </Card>

        {loading ? (
          <div className="space-y-4">
            <Skeleton className="h-40 w-full" />
            <Skeleton className="h-40 w-full" />
            <Skeleton className="h-40 w-full" />
          </div>
        ) : loadError ? (
          <Alert tone="error" title="Couldn't load roles">
            {loadError}
            <div className="mt-3">
              <Button type="button" variant="outline" size="sm" onClick={() => void load(appliedFilters)}>
                Try again
              </Button>
            </div>
          </Alert>
        ) : jobs.length === 0 ? (
          <EmptyState
            icon={<Briefcase className="size-6" />}
            title={hasActiveFilters ? "No roles match these filters" : "No roles published yet"}
            description={
              hasActiveFilters
                ? "Try broadening your search or clearing a filter."
                : "Check back soon — new roles appear here as employers publish them."
            }
            action={
              hasActiveFilters ? (
                <Button variant="outline" onClick={onClear}>
                  Clear filters
                </Button>
              ) : undefined
            }
          />
        ) : (
          <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {jobs.map((job) => (
              <li key={job.id}>
                <Card interactive className="flex h-full flex-col gap-3 p-6">
                  <div className="flex items-start gap-3">
                    <span
                      className="flex size-11 shrink-0 items-center justify-center rounded-md bg-primary-subtle text-primary"
                      aria-hidden="true"
                    >
                      <Building2 className="size-5" />
                    </span>
                    <div className="min-w-0 flex-1 space-y-1">
                      <h2 className="text-h4 text-foreground">
                        <Link
                          href={`/dashboard/discover/${job.id}`}
                          className="rounded-sm after:absolute after:inset-0 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                        >
                          {job.title}
                        </Link>
                      </h2>
                      <p className="truncate text-small font-medium text-foreground-secondary">{job.companyName}</p>
                    </div>
                  </div>

                  <ul className="flex flex-wrap items-center gap-x-4 gap-y-1.5 text-small text-muted-foreground">
                    <li className="flex items-center gap-1.5">
                      <MapPin className="size-4 shrink-0" aria-hidden="true" />
                      {job.location} &middot; {job.workplaceType}
                    </li>
                    <li className="flex items-center gap-1.5">
                      <Briefcase className="size-4 shrink-0" aria-hidden="true" />
                      {job.employmentType}
                    </li>
                  </ul>

                  <div className="mt-auto flex items-center justify-between gap-3 border-t border-border pt-3">
                    <div>
                      {job.compensation ? (
                        <p className="text-small font-medium text-foreground">{job.compensation}</p>
                      ) : null}
                      <p className="text-caption text-muted-foreground">Posted {formatDate(job.publishedAt)}</p>
                    </div>
                    <Button size="sm" variant="outline" asChild className="relative z-10">
                      <Link href={`/dashboard/discover/${job.id}`} tabIndex={-1}>
                        View role
                      </Link>
                    </Button>
                  </div>
                </Card>
              </li>
            ))}
          </ul>
        )}
      </div>
    </DashboardShell>
  );
}
