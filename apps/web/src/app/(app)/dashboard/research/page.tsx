"use client";

import * as React from "react";
import Link from "next/link";
import {
  Bell,
  Bookmark,
  FileText,
  FlaskConical,
  GraduationCap,
  LayoutDashboard,
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
import { formError } from "@/lib/auth/client";
import { listResearchMatches } from "@/lib/aidx/matches-api";
import type { ResearchMatch } from "@/lib/aidx/types";

const NAV: DashboardNavItem[] = [
  { href: "/dashboard", label: "Overview", icon: LayoutDashboard },
  { href: "/dashboard/discover", label: "Discover roles", icon: Search },
  { href: "/dashboard/matches", label: "Job matches", icon: Target },
  { href: "/dashboard/research", label: "Research", icon: FlaskConical },
  { href: "/dashboard/applications", label: "Applications", icon: FileText, badge: "12" },
  { href: "/dashboard/saved", label: "Saved roles", icon: Bookmark, badge: "8" },
  { href: "/dashboard/interviews", label: "Interviews", icon: User, badge: "2" },
  { href: "/dashboard/prepare", label: "Prepare", icon: GraduationCap },
  { href: "/dashboard/notifications", label: "Notifications", icon: Bell, badge: "3" },
];

const QUALITY_COPY: Record<ResearchMatch["matchQuality"], { label: string; tone: "success" | "info" | "neutral" }> = {
  StrongMatch: { label: "Strong match", tone: "success" },
  PartialMatch: { label: "Partial match", tone: "info" },
  ExploreMatch: { label: "Worth exploring", tone: "neutral" },
  Unscored: { label: "Not yet scoreable", tone: "neutral" },
};

export default function ResearchBridgePage() {
  const { getToken } = useAuth();
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [matches, setMatches] = React.useState<ResearchMatch[]>([]);

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      setMatches(await listResearchMatches(token));
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

  const noSkillsRecorded = !loading && !loadError && matches.every((m) => m.matchedSkills.length === 0 && m.skillCoverage !== null);

  return (
    <DashboardShell nav={NAV} navLabel="Applicant dashboard">
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Research-to-Career Bridge</h1>
          <p className="text-body text-muted-foreground">
            Published AIDX Lab research projects, ranked against the skills on your profile. A project is
            information to read; an open opportunity is something you can actually apply to — each is shown
            separately below.
          </p>
        </div>

        {noSkillsRecorded ? (
          <Alert tone="info" title="Add skills to your profile for a real match">
            These projects are shown, but nothing is marked as matched yet because your profile has no recorded
            skills.
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
          <Alert tone="error" title="Couldn't load research matches">
            {loadError}
            <div className="mt-3">
              <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
                Try again
              </Button>
            </div>
          </Alert>
        ) : matches.length === 0 ? (
          <EmptyState
            icon={<FlaskConical className="size-6" />}
            title="No published research projects yet"
            description="Check back soon — AIDX Lab projects appear here once published."
          />
        ) : (
          <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {matches.map((match) => {
              const quality = QUALITY_COPY[match.matchQuality];
              return (
                <li key={match.projectId}>
                  <Card className="flex h-full flex-col gap-3 p-6">
                    <div className="flex items-start gap-3">
                      <span
                        className="flex size-11 shrink-0 items-center justify-center rounded-md bg-primary-subtle text-primary"
                        aria-hidden="true"
                      >
                        <FlaskConical className="size-5" />
                      </span>
                      <div className="min-w-0 flex-1 space-y-1">
                        <h2 className="text-h4 text-foreground">{match.title}</h2>
                        {match.researchAreas.length > 0 ? (
                          <p className="truncate text-small font-medium text-foreground-secondary">
                            {match.researchAreas.join(", ")}
                          </p>
                        ) : null}
                      </div>
                      <Badge tone={quality.tone} variant="subtle">
                        {quality.label}
                        {match.skillCoverage !== null ? ` · ${Math.round(match.skillCoverage * 100)}%` : ""}
                      </Badge>
                    </div>

                    <p className="text-small text-foreground-secondary">{match.shortDescription}</p>

                    {match.skillCoverage === null ? (
                      <p className="text-small text-muted-foreground">
                        This project hasn&apos;t listed specific technologies, so it can&apos;t be scored — open it to
                        read the full description.
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

                    <div className="mt-auto space-y-3 border-t border-border pt-3">
                      {match.openOpportunities.length > 0 ? (
                        <div className="space-y-1.5">
                          <p className="text-caption font-medium text-foreground">
                            {match.openOpportunities.length === 1 ? "Open opportunity" : "Open opportunities"}
                          </p>
                          <ul className="space-y-1">
                            {match.openOpportunities.map((opportunity) => (
                              <li key={opportunity.jobId}>
                                <Link
                                  href={`/aidx/opportunities/${opportunity.jobId}`}
                                  className="text-small font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                                >
                                  {opportunity.title} — view and apply
                                </Link>
                              </li>
                            ))}
                          </ul>
                        </div>
                      ) : (
                        <p className="text-caption text-muted-foreground">No open role on this project right now.</p>
                      )}
                      <Button size="sm" variant="outline" asChild>
                        <Link href={`/aidx/projects/${match.slug}`}>View project details</Link>
                      </Button>
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
