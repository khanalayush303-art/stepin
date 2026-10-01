"use client";

import * as React from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  Bell,
  Bookmark,
  Briefcase,
  Building2,
  ExternalLink,
  FileText,
  GraduationCap,
  LayoutDashboard,
  MapPin,
  Search,
  User,
} from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { DashboardShell, type DashboardNavItem } from "@/components/dashboard/dashboard-shell";
import { SaveJobButton } from "@/components/jobs/save-job-button";
import { ApiError, formError } from "@/lib/auth/client";
import { getApplicationEligibility } from "@/lib/applications/api";
import { getPublicJob } from "@/lib/jobs/api";
import type { PublicJob } from "@/lib/jobs/types";
import { listSavedJobs } from "@/lib/savedJobs/api";
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

export default function JobDetailsPage() {
  const params = useParams<{ id: string }>();
  const { getToken } = useAuth();
  const [job, setJob] = React.useState<PublicJob | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [notFound, setNotFound] = React.useState(false);
  const [hasApplied, setHasApplied] = React.useState(false);
  const [existingApplicationId, setExistingApplicationId] = React.useState<string>();
  const [isSaved, setIsSaved] = React.useState(false);

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    setNotFound(false);
    try {
      const [loadedJob, eligibility] = await Promise.all([
        getPublicJob(params.id),
        getToken().then((token) => getApplicationEligibility(token, params.id)),
      ]);
      setJob(loadedJob);
      setHasApplied(eligibility.hasApplied);
      setExistingApplicationId(eligibility.applicationId ?? undefined);

      // No single-job saved-state endpoint exists, so membership is checked
      // against the full saved-jobs list — same constraint documented on
      // SaveJobButton. Best-effort and separate from the error path above:
      // a failure here shouldn't block the rest of the page from loading.
      try {
        const token = await getToken();
        const saved = await listSavedJobs(token);
        setIsSaved(saved.some((s) => s.jobId === params.id));
      } catch {
        setIsSaved(false);
      }
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        setNotFound(true);
      } else {
        setLoadError(formError(error));
      }
    } finally {
      setLoading(false);
    }
  }, [params.id, getToken]);

  React.useEffect(() => {
    // Fetch-on-mount: same documented pattern as every other Phase 2.1/2.2/2.3 page.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  return (
    <DashboardShell nav={NAV} navLabel="Applicant dashboard">
      <div className="max-w-3xl space-y-6">
        <Link
          href="/dashboard/discover"
          className="text-small font-medium text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          &larr; Back to discover roles
        </Link>

        {loading ? (
          <div className="space-y-4">
            <Skeleton className="h-10 w-2/3" />
            <Skeleton className="h-48 w-full" />
          </div>
        ) : notFound ? (
          <Alert tone="error" title="Role not found">
            This role isn&rsquo;t available — it may have been unpublished or the link may be incorrect.
          </Alert>
        ) : loadError ? (
          <Alert tone="error" title="Couldn't load this role">
            {loadError}
            <div className="mt-3">
              <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
                Try again
              </Button>
            </div>
          </Alert>
        ) : job ? (
          <>
            <div className="space-y-3">
              <div className="flex items-start gap-3">
                <span
                  className="flex size-12 shrink-0 items-center justify-center rounded-md bg-primary-subtle text-primary"
                  aria-hidden="true"
                >
                  <Building2 className="size-6" />
                </span>
                <div className="space-y-1">
                  <h1 className="text-h2 text-foreground">{job.title}</h1>
                  <p className="text-body font-medium text-foreground-secondary">{job.companyName}</p>
                </div>
              </div>

              <ul className="flex flex-wrap items-center gap-x-5 gap-y-2 text-small text-muted-foreground">
                <li className="flex items-center gap-1.5">
                  <MapPin className="size-4 shrink-0" aria-hidden="true" />
                  {job.location} &middot; {job.workplaceType}
                </li>
                <li className="flex items-center gap-1.5">
                  <Briefcase className="size-4 shrink-0" aria-hidden="true" />
                  {job.employmentType}
                </li>
              </ul>
              <p className="text-caption text-muted-foreground">Posted {formatDate(job.publishedAt)}</p>
            </div>

            <Card className="space-y-4 p-6">
              <div className="flex flex-wrap items-center justify-between gap-4 border-b border-border pb-4">
                <div>
                  {job.compensation ? (
                    <p className="text-h4 text-foreground">{job.compensation}</p>
                  ) : (
                    <p className="text-small text-muted-foreground">Compensation not listed</p>
                  )}
                </div>
                <div className="flex items-center gap-2">
                  <SaveJobButton jobId={job.id} saved={isSaved} onSavedChange={setIsSaved} />
                  {hasApplied ? (
                    <Button variant="outline" asChild>
                      <Link href={`/dashboard/applications/${existingApplicationId}`}>Already applied &middot; View application</Link>
                    </Button>
                  ) : (
                    <Button asChild>
                      <Link href={`/dashboard/discover/${job.id}/apply`}>Apply</Link>
                    </Button>
                  )}
                </div>
              </div>

              <div className="space-y-2">
                <h2 className="text-h4 text-foreground">About this role</h2>
                <p className="whitespace-pre-line text-body text-foreground-secondary">{job.description}</p>
              </div>

              {job.skills.length > 0 ? (
                <div className="space-y-2">
                  <h2 className="text-h4 text-foreground">Skills</h2>
                  <ul className="flex flex-wrap gap-2">
                    {job.skills.map((skill) => (
                      <li key={skill} className="rounded-sm bg-muted px-2 py-1 text-caption text-foreground-secondary">
                        {skill}
                      </li>
                    ))}
                  </ul>
                </div>
              ) : null}
            </Card>

            {job.companyDescription || job.companyWebsite || job.companyIndustry || job.companyLocation ? (
              <Card className="space-y-3 p-6">
                <h2 className="text-h4 text-foreground">About {job.companyName}</h2>
                {job.companyDescription ? (
                  <p className="text-small text-foreground-secondary">{job.companyDescription}</p>
                ) : null}
                <dl className="grid gap-2 text-small sm:grid-cols-2">
                  {job.companyIndustry ? (
                    <div className="flex justify-between gap-3 sm:justify-start sm:gap-2">
                      <dt className="text-muted-foreground">Industry</dt>
                      <dd className="font-medium text-foreground">{job.companyIndustry}</dd>
                    </div>
                  ) : null}
                  {job.companyLocation ? (
                    <div className="flex justify-between gap-3 sm:justify-start sm:gap-2">
                      <dt className="text-muted-foreground">Location</dt>
                      <dd className="font-medium text-foreground">{job.companyLocation}</dd>
                    </div>
                  ) : null}
                </dl>
                {job.companyWebsite ? (
                  <a
                    href={job.companyWebsite}
                    target="_blank"
                    rel="noreferrer"
                    className="inline-flex items-center gap-1.5 text-small font-medium text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                  >
                    Visit website
                    <ExternalLink className="size-3.5" aria-hidden="true" />
                  </a>
                ) : null}
              </Card>
            ) : null}
          </>
        ) : null}
      </div>
    </DashboardShell>
  );
}
