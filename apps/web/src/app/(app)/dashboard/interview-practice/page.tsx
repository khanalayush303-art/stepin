"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Mic, Trash2 } from "lucide-react";
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
import { createInterviewSession, deleteInterviewSession, getInterviewTemplates, listInterviewSessions } from "@/lib/interview/api";
import type { InterviewCategory, InterviewSessionSummary, InterviewTemplates } from "@/lib/interview/types";

const NAV = CANDIDATE_NAV;

const STATUS_TONE: Record<InterviewSessionSummary["status"], "success" | "info"> = {
  InProgress: "info",
  Completed: "success",
};

export default function InterviewPracticePage() {
  const { getToken } = useAuth();
  const router = useRouter();

  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [sessions, setSessions] = React.useState<InterviewSessionSummary[]>([]);
  const [templates, setTemplates] = React.useState<InterviewTemplates | null>(null);

  const [category, setCategory] = React.useState<InterviewCategory>("Behavioral");
  const [targetRole, setTargetRole] = React.useState("");
  const [questionCount, setQuestionCount] = React.useState(5);
  const [fieldErrors, setFieldErrors] = React.useState<Record<string, string[]>>({});
  const [starting, setStarting] = React.useState(false);
  const [startError, setStartError] = React.useState<string>();
  const [deletingId, setDeletingId] = React.useState<string | null>(null);

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      const [loadedSessions, loadedTemplates] = await Promise.all([listInterviewSessions(token), getInterviewTemplates(token)]);
      setSessions(loadedSessions);
      setTemplates(loadedTemplates);
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

  const categoryOption = templates?.categories.find((c) => c.category === category);
  const maxForCategory = Math.min(templates?.maxQuestionCount ?? 8, categoryOption?.availableQuestionCount ?? 8);

  async function onStart(event: React.FormEvent) {
    event.preventDefault();
    setFieldErrors({});
    setStartError(undefined);
    setStarting(true);
    try {
      const token = await getToken();
      const session = await createInterviewSession(token, {
        category,
        targetRole: targetRole.trim() || null,
        questionCount,
      });
      router.push(`/dashboard/interview-practice/${session.id}`);
    } catch (error) {
      if (error instanceof ApiError && error.problem.errors) {
        setFieldErrors(error.problem.errors);
      }
      setStartError(formError(error));
    } finally {
      setStarting(false);
    }
  }

  async function onDelete(id: string) {
    if (!window.confirm("Delete this practice session? This can't be undone.")) {
      return;
    }
    setDeletingId(id);
    try {
      const token = await getToken();
      await deleteInterviewSession(token, id);
      setSessions((current) => current.filter((s) => s.id !== id));
    } catch (error) {
      setStartError(formError(error));
    } finally {
      setDeletingId(null);
    }
  }

  return (
    <DashboardShell nav={NAV} navLabel="Applicant dashboard">
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Interview Practice Studio</h1>
          <p className="text-body text-muted-foreground">
            Practice with curated questions and get rubric-based practice feedback — simple, documented heuristics,
            not an AI-generated assessment. Use your own genuine examples.
          </p>
        </div>

        <Card className="p-4">
          <form onSubmit={onStart} className="flex flex-wrap items-end gap-3">
            <div className="min-w-[180px]">
              <span className="mb-1.5 block text-small font-medium text-foreground">Category</span>
              <Select
                value={category}
                onValueChange={(v) => {
                  setCategory(v as InterviewCategory);
                  setQuestionCount(5);
                }}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(templates?.categories ?? []).map((option) => (
                    <SelectItem key={option.category} value={option.category}>
                      {option.category}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="min-w-[220px] flex-1">
              <FormField id="targetRole" label="Target role (optional)" error={fieldErrors.targetRole?.[0]}>
                {(props) => (
                  <Input {...props} value={targetRole} onChange={(e) => setTargetRole(e.target.value)} placeholder="e.g. Graduate Software Engineer" />
                )}
              </FormField>
              <p className="mt-1 text-caption text-muted-foreground">
                This is metadata only — questions come from the same general question bank regardless of role.
              </p>
            </div>

            <div className="min-w-[140px]">
              <FormField id="questionCount" label={`Questions (${templates?.minQuestionCount ?? 3}–${maxForCategory})`} error={fieldErrors.questionCount?.[0]}>
                {(props) => (
                  <Input
                    {...props}
                    type="number"
                    min={templates?.minQuestionCount ?? 3}
                    max={maxForCategory}
                    value={questionCount}
                    onChange={(e) => setQuestionCount(Number(e.target.value))}
                  />
                )}
              </FormField>
            </div>

            <Button type="submit" disabled={starting || !templates}>
              {starting ? "Starting…" : "Start practice session"}
            </Button>
          </form>
          {startError ? <p className="mt-2 text-small text-error">{startError}</p> : null}
        </Card>

        {loading ? (
          <div className="space-y-4">
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
          </div>
        ) : loadError ? (
          <Alert tone="error" title="Couldn't load your practice sessions">
            {loadError}
            <div className="mt-3">
              <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
                Try again
              </Button>
            </div>
          </Alert>
        ) : sessions.length === 0 ? (
          <EmptyState
            icon={<Mic className="size-6" />}
            title="No practice sessions yet"
            description="Start one above to begin practicing."
          />
        ) : (
          <div className="space-y-3">
            <h2 className="text-h3 text-foreground">Your sessions</h2>
            <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
              {sessions.map((session) => (
                <li key={session.id}>
                  <Card interactive className="flex h-full flex-col gap-3 p-6">
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0 flex-1 space-y-1">
                        <h3 className="text-h4 text-foreground">
                          <Link
                            href={`/dashboard/interview-practice/${session.id}`}
                            className="rounded-sm after:absolute after:inset-0 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                          >
                            {session.category}
                            {session.targetRole ? ` — ${session.targetRole}` : ""}
                          </Link>
                        </h3>
                        <p className="text-small text-muted-foreground">
                          {session.answeredCount} of {session.questionCount} answered
                        </p>
                      </div>
                      <Badge tone={STATUS_TONE[session.status]} variant="subtle" className="shrink-0">
                        {session.status === "InProgress" ? "In progress" : "Completed"}
                      </Badge>
                    </div>

                    <div className="relative z-10 mt-auto flex items-center gap-2">
                      <Button size="sm" variant="outline" asChild>
                        <Link href={`/dashboard/interview-practice/${session.id}`} tabIndex={-1}>
                          {session.status === "InProgress" ? "Continue" : "Review"}
                        </Link>
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        className="ml-auto text-error"
                        disabled={deletingId === session.id}
                        onClick={() => void onDelete(session.id)}
                      >
                        <Trash2 className="size-4" />
                      </Button>
                    </div>
                  </Card>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </DashboardShell>
  );
}
