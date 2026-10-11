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
  Target,
  User,
} from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { DashboardShell, type DashboardNavItem } from "@/components/dashboard/dashboard-shell";
import { SaveJobButton } from "@/components/jobs/save-job-button";
import { formError } from "@/lib/auth/client";
import { listJobMatches } from "@/lib/jobs/api";
import type { JobMatch, JobMatchQuality } from "@/lib/jobs/types";
import { listSavedJobs } from "@/lib/savedJobs/api";
import { formatDate } from "@/lib/utils";

const NAV: DashboardNavItem[] = [
  { href: "/dashboard", label: "Overview", icon: LayoutDashboard },
  { href: "/dashboard/discover", label: "Discover roles", icon: Search },
  { href: "/dashboard/matches", label: "Job matches", icon: Target },
  { href: "/dashboard/applications", label: "Applications", icon: FileText, badge: "12" },
  { href: "/dashboard/saved", label: "Saved roles", icon: Bookmark, badge: "8" },
  { href: "/dashboard/interviews", label: "Interviews", icon: User, badge: "2" },
  { href: "/dashboard/prepare", label: "Prepare", icon: GraduationCap },
  { href: "/dashboard/notifications", label: "Notifications", icon: Bell, badge: "3" },
];

/**
 * How each quality label reads to a candidate, and the badge tone that goes with it. These are
 * display labels only — the underlying rule (skill coverage against documented thresholds) is
 * computed once, server-side, in JobMatchScorer. Nothing here recomputes or overrides it.
 */
const QUALITY_COPY: Record<JobMatchQuality, { label: string; tone: "success" | "info" | "neutral" }> = {
  StrongMatch: { label: "Strong match", tone: "success" },
  PartialMatch: { label: "Partial match", tone: "info" },
  ExploreMatch: { label: "Worth exploring", tone: "neutral" },
  Unscored: { label: "Not yet scoreable", tone: "neutral" },
};

export default function JobMatchesPage() {
  const { getToken } = useAuth();
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [matches, setMatches] = React.useState<JobMatch[]>([]);
  const [savedJobIds, setSavedJobIds] = React.useState<Set<string>>(new Set());

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      setMatches(await listJobMatches(token));
    } catch (error) {
      setLoadError(formError(error));
    } finally {
      setLoading(false);
    }
  }, [getToken]);

  React.useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  React.useEffect(() => {
    // Same constraint as Discover: no single-job saved-state endpoint, so membership is
    // checked against the full saved-jobs list. Independent of the match list itself.
    async function loadSaved() {
      try {
        const token = await getToken();
        const saved = await listSavedJobs(token);
        setSavedJobIds(new Set(saved.map((s) => s.jobId)));
      } catch {
        // Best-effort: every card just starts "not saved" if this fails.
      }
    }

    void loadSaved();
  }, [getToken]);

  function onSavedChange(jobId: string, saved: boolean) {
    setSavedJobIds((current) => {
      const next = new Set(current);
      if (saved) next.add(jobId);
      else next.delete(jobId);
      return next;
    });
  }

  const noSkillsRecorded = !loading && !loadError && matches.every((m) => m.matchedSkills.length === 0 && m.skillCoverage !== null);

  return (
    <DashboardShell nav={NAV} navLabel="Applicant dashboard">
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Job matches</h1>
          <p className="text-body text-muted-foreground">
            Published roles ranked against the skills on your profile, with a plain explanation for every match.
          </p>
        </div>

        {noSkillsRecorded ? (
          <Alert tone="info" title="Add skills to your profile for a real match">
            These roles are shown, but nothing is marked as matched yet because your profile has no recorded skills.
            <div className="mt-3">
              <Button asChild size="sm" variant="outline">
                <Link href="/dashboard/profile">Update your profile</Link>
              </Button>
            </div>
          </Alert>
        ) : null}

        {loading ? (
          <div className="space-y-4">
            <Skeleton className="h-40 w-full" />
            <Skeleton className="h-40 w-full" />
            <Skeleton className="h-40 w-full" />
          </div>
        ) : loadError ? (
          <Alert tone="error" title="Couldn't load job matches">
            {loadError}
            <div className="mt-3">
              <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
                Try again
              </Button>
            </div>
          </Alert>
        ) : matches.length === 0 ? (
          <EmptyState
            icon={<Target className="size-6" />}
            title="No published roles to match against yet"
            description="Check back soon — new roles appear here as employers publish them."
          />
        ) : (
          <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {matches.map((match) => {
              const quality = QUALITY_COPY[match.matchQuality];
              return (
                <li key={match.id}>
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
                            href={`/dashboard/discover/${match.id}`}
                            className="rounded-sm after:absolute after:inset-0 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                          >
                            {match.title}
                          </Link>
                        </h2>
                        <p className="truncate text-small font-medium text-foreground-secondary">{match.companyName}</p>
                      </div>
                      <Badge tone={quality.tone} variant="subtle">
                        {quality.label}
                        {match.skillCoverage !== null ? ` · ${Math.round(match.skillCoverage * 100)}%` : ""}
                      </Badge>
                    </div>

                    <ul className="flex flex-wrap items-center gap-x-4 gap-y-1.5 text-small text-muted-foreground">
                      <li className="flex items-center gap-1.5">
                        <MapPin className="size-4 shrink-0" aria-hidden="true" />
                        {match.location} &middot; {match.workplaceType}
                      </li>
                      <li className="flex items-center gap-1.5">
                        <Briefcase className="size-4 shrink-0" aria-hidden="true" />
                        {match.employmentType}
                      </li>
                    </ul>

                    {match.skillCoverage === null ? (
                      <p className="text-small text-muted-foreground">
                        This role hasn&apos;t listed specific skills, so it can&apos;t be scored — open it to read the full description.
                      </p>
                    ) : (
                      <div className="space-y-2 text-small">
                        {match.matchedSkills.length > 0 ? (
                          <p>
                            <span className="font-medium text-foreground">You already have: </span>
                            <span className="text-foreground-secondary">{match.matchedSkills.join(", ")}</span>
                          </p>
                        ) : null}
                        {match.skillsNotYetDemonstrated.length > 0 ? (
                          <p>
                            <span className="font-medium text-foreground">Not yet on your profile: </span>
                            <span className="text-muted-foreground">{match.skillsNotYetDemonstrated.join(", ")}</span>
                          </p>
                        ) : null}
                      </div>
                    )}

                    <div className="mt-auto flex items-center justify-between gap-3 border-t border-border pt-3">
                      <div>
                        {match.compensation ? (
                          <p className="text-small font-medium text-foreground">{match.compensation}</p>
                        ) : null}
                        <p className="text-caption text-muted-foreground">Posted {formatDate(match.publishedAt)}</p>
                      </div>
                      <div className="flex items-center gap-2">
                        <SaveJobButton
                          jobId={match.id}
                          saved={savedJobIds.has(match.id)}
                          onSavedChange={(saved) => onSavedChange(match.id, saved)}
                        />
                        <Button size="sm" variant="outline" asChild className="relative z-10">
                          <Link href={`/dashboard/discover/${match.id}`} tabIndex={-1}>
                            View role
                          </Link>
                        </Button>
                      </div>
                    </div>
                  </Card>
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </DashboardShell>
  );
}
