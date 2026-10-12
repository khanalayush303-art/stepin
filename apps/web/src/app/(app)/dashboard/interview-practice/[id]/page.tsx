"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { ArrowLeft, ArrowRight, CheckCircle2, Circle, Trash2 } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { CANDIDATE_NAV } from "@/components/dashboard/candidate-nav";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { formError } from "@/lib/auth/client";
import { completeInterviewSession, deleteInterviewSession, getInterviewSession, saveInterviewAnswer } from "@/lib/interview/api";
import type { InterviewSession } from "@/lib/interview/types";

const NAV = CANDIDATE_NAV;

const BAND_TONE: Record<string, "neutral" | "warning" | "success" | "info"> = {
  "Not yet answered": "neutral",
  "Needs more detail": "warning",
  "Covers the basics": "info",
  "Well-structured": "success",
};

export default function InterviewSessionDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { getToken } = useAuth();
  const router = useRouter();

  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [session, setSession] = React.useState<InterviewSession | null>(null);
  const [activeIndex, setActiveIndex] = React.useState(0);
  const [draft, setDraft] = React.useState("");
  const [saving, setSaving] = React.useState(false);
  const [saveError, setSaveError] = React.useState<string>();
  const [savedJustNow, setSavedJustNow] = React.useState(false);
  const [completing, setCompleting] = React.useState(false);
  const [actionError, setActionError] = React.useState<string>();

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      const loaded = await getInterviewSession(token, id);
      setSession(loaded);
      setActiveIndex(0);
      setDraft(loaded.questions[0]?.answerText ?? "");
    } catch (error) {
      setLoadError(formError(error));
    } finally {
      setLoading(false);
    }
  }, [getToken, id]);

  React.useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  const question = session?.questions[activeIndex] ?? null;
  const isReadOnly = session?.status === "Completed";

  function goTo(index: number) {
    if (!session) return;
    setActiveIndex(index);
    setDraft(session.questions[index]?.answerText ?? "");
    setSaveError(undefined);
    setSavedJustNow(false);
  }

  async function onSaveAnswer() {
    if (!session || !question || isReadOnly) return;
    setSaving(true);
    setSaveError(undefined);
    setSavedJustNow(false);
    try {
      const token = await getToken();
      const updated = await saveInterviewAnswer(token, session.id, question.id, draft.trim() || null);
      setSession((current) => {
        if (!current) return current;
        const questions = current.questions.map((q) => (q.id === updated.id ? updated : q));
        return { ...current, questions, answeredCount: questions.filter((q) => q.answerText).length };
      });
      setSavedJustNow(true);
    } catch (error) {
      setSaveError(formError(error));
    } finally {
      setSaving(false);
    }
  }

  async function onSaveAndNext() {
    await onSaveAnswer();
    if (session && activeIndex < session.questions.length - 1) {
      goTo(activeIndex + 1);
    }
  }

  async function onComplete() {
    if (!session) return;
    if (!window.confirm("Complete this session? You won't be able to edit answers after this.")) {
      return;
    }
    setCompleting(true);
    setActionError(undefined);
    try {
      const token = await getToken();
      const completed = await completeInterviewSession(token, session.id);
      setSession(completed);
    } catch (error) {
      setActionError(formError(error));
    } finally {
      setCompleting(false);
    }
  }

  async function onDelete() {
    if (!session) return;
    if (!window.confirm("Delete this practice session? This can't be undone.")) {
      return;
    }
    try {
      const token = await getToken();
      await deleteInterviewSession(token, session.id);
      router.push("/dashboard/interview-practice");
    } catch (error) {
      setActionError(formError(error));
    }
  }

  if (loading) {
    return (
      <DashboardShell nav={NAV} navLabel="Applicant dashboard">
        <div className="space-y-4">
          <Skeleton className="h-10 w-1/2" />
          <Skeleton className="h-64 w-full" />
        </div>
      </DashboardShell>
    );
  }

  if (loadError || !session || !question) {
    return (
      <DashboardShell nav={NAV} navLabel="Applicant dashboard">
        <Alert tone="error" title="Couldn't load this session">
          {loadError ?? "It may not exist, or it may not be yours."}
          <div className="mt-3">
            <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
              Try again
            </Button>
          </div>
        </Alert>
      </DashboardShell>
    );
  }

  return (
    <DashboardShell nav={NAV} navLabel="Applicant dashboard">
      <div className="space-y-6">
        <Link
          href="/dashboard/interview-practice"
          className="inline-flex items-center gap-1.5 text-small font-medium text-foreground-secondary hover:text-foreground"
        >
          <ArrowLeft className="size-4" />
          All practice sessions
        </Link>

        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="space-y-1">
            <h1 className="text-h2 text-foreground">
              {session.category}
              {session.targetRole ? ` — ${session.targetRole}` : ""}
            </h1>
            <p className="text-small text-muted-foreground">
              {session.answeredCount} of {session.questionCount} answered
            </p>
          </div>
          <div className="flex items-center gap-2">
            <Badge tone={session.status === "InProgress" ? "info" : "success"} variant="subtle">
              {session.status === "InProgress" ? "In progress" : "Completed"}
            </Badge>
            {session.status === "InProgress" ? (
              <Button size="sm" variant="outline" onClick={() => void onComplete()} disabled={completing}>
                {completing ? "Completing…" : "Complete session"}
              </Button>
            ) : null}
            <Button size="sm" variant="ghost" className="text-error" onClick={() => void onDelete()}>
              <Trash2 className="size-4" />
            </Button>
          </div>
        </div>

        {isReadOnly ? (
          <Alert tone="info" title="This session is completed">
            Answers can no longer be edited. Start a new session to practice again.
          </Alert>
        ) : null}

        {actionError ? <p className="text-small text-error">{actionError}</p> : null}

        {/* Question navigator */}
        <div className="flex flex-wrap gap-1.5">
          {session.questions.map((q, index) => (
            <button
              key={q.id}
              type="button"
              onClick={() => goTo(index)}
              aria-current={index === activeIndex}
              className={`flex size-8 items-center justify-center rounded-full border text-small font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring ${
                index === activeIndex
                  ? "border-primary bg-primary text-primary-foreground"
                  : q.answerText
                    ? "border-success text-success"
                    : "border-border text-muted-foreground hover:border-border-strong"
              }`}
            >
              {q.answerText ? <CheckCircle2 className="size-4" /> : <Circle className="size-4" />}
            </button>
          ))}
        </div>

        <Card className="space-y-4 p-6">
          <div className="space-y-1">
            <p className="text-caption font-medium uppercase tracking-wide text-muted-foreground">
              Question {activeIndex + 1} of {session.questions.length}
            </p>
            <h2 className="text-h3 text-foreground">{question.questionText}</h2>
          </div>

          <Textarea
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            placeholder="Write your answer using a genuine example…"
            rows={6}
            readOnly={isReadOnly}
            maxLength={4000}
          />
          <p className="text-caption text-muted-foreground">{draft.length} / 4000 characters</p>

          {saveError ? <p className="text-small text-error">{saveError}</p> : null}
          {savedJustNow ? <p className="text-small text-success">Saved.</p> : null}

          {!isReadOnly ? (
            <div className="flex flex-wrap items-center gap-2">
              <Button type="button" variant="outline" onClick={() => void onSaveAnswer()} disabled={saving}>
                {saving ? "Saving…" : "Save answer"}
              </Button>
              {activeIndex < session.questions.length - 1 ? (
                <Button type="button" onClick={() => void onSaveAndNext()} disabled={saving}>
                  Save and next
                  <ArrowRight className="size-4" />
                </Button>
              ) : null}
              <div className="ml-auto flex items-center gap-2">
                <Button type="button" variant="ghost" disabled={activeIndex === 0} onClick={() => goTo(activeIndex - 1)}>
                  Previous
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  disabled={activeIndex === session.questions.length - 1}
                  onClick={() => goTo(activeIndex + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          ) : (
            <div className="flex items-center gap-2">
              <Button type="button" variant="ghost" disabled={activeIndex === 0} onClick={() => goTo(activeIndex - 1)}>
                Previous
              </Button>
              <Button
                type="button"
                variant="ghost"
                disabled={activeIndex === session.questions.length - 1}
                onClick={() => goTo(activeIndex + 1)}
              >
                Next
              </Button>
            </div>
          )}

          {/* Feedback */}
          <div className="space-y-3 rounded-lg border border-border bg-surface-subtle p-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h3 className="text-body font-medium text-foreground">Practice feedback</h3>
              <Badge tone={BAND_TONE[question.feedback.band] ?? "neutral"} variant="subtle">
                {question.feedback.band}
              </Badge>
            </div>

            {question.feedback.answered ? (
              <>
                <ul className="space-y-1.5">
                  {question.feedback.criteria.map((criterion) => (
                    <li key={criterion.name} className="flex items-start gap-2 text-small">
                      {criterion.met ? (
                        <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-success" />
                      ) : (
                        <Circle className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
                      )}
                      <div>
                        <span className={criterion.met ? "text-foreground" : "font-medium text-foreground"}>{criterion.name}</span>
                        {!criterion.met ? <p className="text-caption text-muted-foreground">{criterion.note}</p> : null}
                      </div>
                    </li>
                  ))}
                </ul>
                <p className="text-small text-foreground-secondary">
                  <span className="font-medium text-foreground">Suggested structure: </span>
                  {question.feedback.suggestedStructure}
                </p>
              </>
            ) : (
              <p className="text-small text-muted-foreground">Save an answer to see feedback here.</p>
            )}

            <p className="text-caption text-muted-foreground">{question.feedback.disclaimer}</p>
          </div>
        </Card>
      </div>
    </DashboardShell>
  );
}
