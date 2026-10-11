"use client";

import * as React from "react";
import Link from "next/link";
import { Compass } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { CANDIDATE_NAV } from "@/components/dashboard/candidate-nav";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { ApiError, formError } from "@/lib/auth/client";
import { createCareerGoal, listCareerGoals } from "@/lib/career/api";
import type { CareerGoal } from "@/lib/career/types";
import { listPublicJobs } from "@/lib/jobs/api";
import type { PublicJobSummary } from "@/lib/jobs/types";

const NAV = CANDIDATE_NAV;

const STATUS_TONE: Record<CareerGoal["status"], "success" | "info" | "neutral"> = {
  Active: "info",
  Completed: "success",
  Archived: "neutral",
};

const NO_TARGET_JOB = "none";

export default function CareerGpsPage() {
  const { getToken } = useAuth();
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [goals, setGoals] = React.useState<CareerGoal[]>([]);
  const [jobs, setJobs] = React.useState<PublicJobSummary[]>([]);

  const [targetRole, setTargetRole] = React.useState("");
  const [targetDate, setTargetDate] = React.useState("");
  const [targetJobId, setTargetJobId] = React.useState(NO_TARGET_JOB);
  const [fieldErrors, setFieldErrors] = React.useState<Record<string, string[]>>({});
  const [submitting, setSubmitting] = React.useState(false);
  const [submitError, setSubmitError] = React.useState<string>();

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      const [loadedGoals, loadedJobs] = await Promise.all([listCareerGoals(token), listPublicJobs()]);
      setGoals(loadedGoals);
      setJobs(loadedJobs);
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

  async function onCreate(event: React.FormEvent) {
    event.preventDefault();
    setFieldErrors({});
    setSubmitError(undefined);
    setSubmitting(true);
    try {
      const token = await getToken();
      const goal = await createCareerGoal(token, {
        targetRole: targetRole.trim(),
        description: null,
        targetDate: targetDate || null,
        targetJobId: targetJobId === NO_TARGET_JOB ? null : targetJobId,
      });
      setGoals((current) => [goal, ...current]);
      setTargetRole("");
      setTargetDate("");
      setTargetJobId(NO_TARGET_JOB);
    } catch (error) {
      if (error instanceof ApiError && error.problem.errors) {
        setFieldErrors(error.problem.errors);
      }
      setSubmitError(formError(error));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <DashboardShell nav={NAV} navLabel="Applicant dashboard">
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Career GPS</h1>
          <p className="text-body text-muted-foreground">
            Set a target role, break it into a roadmap, and see how your skills compare — a goal works with or
            without a linked job.
          </p>
        </div>

        <Card className="p-4">
          <form onSubmit={onCreate} className="flex flex-wrap items-end gap-3">
            <div className="min-w-[220px] flex-1">
              <FormField id="targetRole" label="Target role" error={fieldErrors.targetRole?.[0]}>
                {(props) => (
                  <Input
                    {...props}
                    value={targetRole}
                    onChange={(e) => setTargetRole(e.target.value)}
                    placeholder="e.g. Graduate Software Engineer"
                    required
                  />
                )}
              </FormField>
            </div>
            <div className="min-w-[160px]">
              <FormField id="targetDate" label="Target date (optional)">
                {(props) => (
                  <Input {...props} type="date" value={targetDate} onChange={(e) => setTargetDate(e.target.value)} />
                )}
              </FormField>
            </div>
            <div className="min-w-[220px]">
              <span className="mb-1.5 block text-small font-medium text-foreground">Target job (optional)</span>
              <Select value={targetJobId} onValueChange={setTargetJobId}>
                <SelectTrigger>
                  <SelectValue placeholder="No specific job" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NO_TARGET_JOB}>No specific job</SelectItem>
                  {jobs.map((job) => (
                    <SelectItem key={job.id} value={job.id}>
                      {job.title} — {job.companyName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {fieldErrors.targetJobId?.[0] ? (
                <p className="mt-1 text-caption text-error">{fieldErrors.targetJobId[0]}</p>
              ) : null}
            </div>
            <Button type="submit" disabled={submitting || !targetRole.trim()}>
              {submitting ? "Adding…" : "Add goal"}
            </Button>
          </form>
          {submitError ? <p className="mt-2 text-small text-error">{submitError}</p> : null}
        </Card>

        {loading ? (
          <div className="space-y-4">
            <Skeleton className="h-32 w-full" />
            <Skeleton className="h-32 w-full" />
          </div>
        ) : loadError ? (
          <Alert tone="error" title="Couldn't load your career goals">
            {loadError}
            <div className="mt-3">
              <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
                Try again
              </Button>
            </div>
          </Alert>
        ) : goals.length === 0 ? (
          <EmptyState
            icon={<Compass className="size-6" />}
            title="No career goals yet"
            description="Add a target role above to start your roadmap."
          />
        ) : (
          <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {goals.map((goal) => (
              <li key={goal.id}>
                <Card interactive className="flex h-full flex-col gap-3 p-6">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0 flex-1 space-y-1">
                      <h2 className="text-h4 text-foreground">
                        <Link
                          href={`/dashboard/career-gps/${goal.id}`}
                          className="rounded-sm after:absolute after:inset-0 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                        >
                          {goal.targetRole}
                        </Link>
                      </h2>
                      {goal.targetJobTitle ? (
                        <p className="truncate text-small text-foreground-secondary">
                          Targeting: {goal.targetJobTitle} at {goal.targetJobCompanyName}
                        </p>
                      ) : null}
                    </div>
                    <div className="flex shrink-0 flex-col items-end gap-1">
                      {goal.isPrimary ? (
                        <Badge tone="brand" variant="subtle">
                          Primary
                        </Badge>
                      ) : null}
                      <Badge tone={STATUS_TONE[goal.status]} variant="subtle">
                        {goal.status}
                      </Badge>
                    </div>
                  </div>

                  {goal.roadmapItemCount > 0 ? (
                    <div className="space-y-1">
                      <div className="h-2 w-full rounded-full bg-muted">
                        <div
                          className="h-2 rounded-full bg-primary transition-[width]"
                          style={{ width: `${goal.progressPercent}%` }}
                        />
                      </div>
                      <p className="text-caption text-muted-foreground">
                        {goal.completedRoadmapItemCount} of {goal.roadmapItemCount} roadmap items complete
                      </p>
                    </div>
                  ) : (
                    <p className="text-small text-muted-foreground">No roadmap items yet.</p>
                  )}

                  <Button size="sm" variant="outline" asChild className="relative z-10 mt-auto w-fit">
                    <Link href={`/dashboard/career-gps/${goal.id}`} tabIndex={-1}>
                      Open roadmap
                    </Link>
                  </Button>
                </Card>
              </li>
            ))}
          </ul>
        )}
      </div>
    </DashboardShell>
  );
}
