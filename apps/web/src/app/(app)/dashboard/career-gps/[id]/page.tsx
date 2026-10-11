"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { ArrowLeft, CheckCircle2, Circle, ListTodo, PlayCircle, Trash2 } from "lucide-react";
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
import {
  archiveCareerGoal,
  completeCareerGoal,
  completeRoadmapItem,
  createRoadmapItem,
  deleteCareerGoal,
  deleteRoadmapItem,
  getCareerGoal,
  getCareerGoalSkillGap,
  listRoadmapItems,
  reactivateCareerGoal,
  reopenRoadmapItem,
  startRoadmapItem,
  updateCareerGoal,
} from "@/lib/career/api";
import type { CareerGoal, CareerGoalSkillGap, RoadmapItem, RoadmapItemPriority } from "@/lib/career/types";

const NAV = CANDIDATE_NAV;

const STATUS_TONE: Record<CareerGoal["status"], "success" | "info" | "neutral"> = {
  Active: "info",
  Completed: "success",
  Archived: "neutral",
};

const ITEM_STATUS_ICON: Record<RoadmapItem["status"], React.ComponentType<{ className?: string }>> = {
  NotStarted: Circle,
  InProgress: PlayCircle,
  Completed: CheckCircle2,
};

const PRIORITY_TONE: Record<RoadmapItemPriority, "neutral" | "warning" | "error"> = {
  Low: "neutral",
  Medium: "warning",
  High: "error",
};

export default function CareerGoalDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { getToken } = useAuth();
  const router = useRouter();

  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [goal, setGoal] = React.useState<CareerGoal | null>(null);
  const [items, setItems] = React.useState<RoadmapItem[]>([]);
  const [skillGap, setSkillGap] = React.useState<CareerGoalSkillGap | null>(null);
  const [actionError, setActionError] = React.useState<string>();

  const [newTitle, setNewTitle] = React.useState("");
  const [newDate, setNewDate] = React.useState("");
  const [newPriority, setNewPriority] = React.useState<RoadmapItemPriority>("Medium");
  const [itemFieldErrors, setItemFieldErrors] = React.useState<Record<string, string[]>>({});
  const [addingItem, setAddingItem] = React.useState(false);

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      const [loadedGoal, loadedItems] = await Promise.all([getCareerGoal(token, id), listRoadmapItems(token, id)]);
      setGoal(loadedGoal);
      setItems(loadedItems);

      if (loadedGoal.targetJobId) {
        try {
          setSkillGap(await getCareerGoalSkillGap(token, id));
        } catch {
          setSkillGap(null);
        }
      } else {
        setSkillGap(null);
      }
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

  async function runAction<T>(action: () => Promise<T>, onSuccess?: (result: T) => void) {
    setActionError(undefined);
    try {
      const result = await action();
      onSuccess?.(result);
    } catch (error) {
      setActionError(formError(error));
    }
  }

  async function onTogglePrimary() {
    if (!goal) return;
    const token = await getToken();
    await runAction(
      () =>
        updateCareerGoal(token, id, {
          targetRole: goal.targetRole,
          description: goal.description,
          targetDate: goal.targetDate,
          targetJobId: goal.targetJobId,
          isPrimary: !goal.isPrimary,
        }),
      setGoal,
    );
  }

  async function onStatusAction(action: "complete" | "archive" | "reactivate") {
    const token = await getToken();
    const call = action === "complete" ? completeCareerGoal : action === "archive" ? archiveCareerGoal : reactivateCareerGoal;
    await runAction(() => call(token, id), setGoal);
  }

  async function onDeleteGoal() {
    if (!window.confirm("Delete this career goal and its entire roadmap? This can't be undone.")) {
      return;
    }
    const token = await getToken();
    try {
      await deleteCareerGoal(token, id);
      router.push("/dashboard/career-gps");
    } catch (error) {
      setActionError(formError(error));
    }
  }

  async function onAddItem(event: React.FormEvent) {
    event.preventDefault();
    setItemFieldErrors({});
    setAddingItem(true);
    try {
      const token = await getToken();
      const item = await createRoadmapItem(token, id, {
        title: newTitle.trim(),
        description: null,
        targetDate: newDate || null,
        priority: newPriority,
      });
      setItems((current) => [...current, item]);
      setNewTitle("");
      setNewDate("");
      setNewPriority("Medium");
      setGoal((current) => (current ? { ...current, roadmapItemCount: current.roadmapItemCount + 1 } : current));
    } catch (error) {
      if (error instanceof ApiError && error.problem.errors) {
        setItemFieldErrors(error.problem.errors);
      }
      setActionError(formError(error));
    } finally {
      setAddingItem(false);
    }
  }

  async function onItemAction(itemId: string, action: "start" | "complete" | "reopen") {
    const token = await getToken();
    const call = action === "start" ? startRoadmapItem : action === "complete" ? completeRoadmapItem : reopenRoadmapItem;
    await runAction(
      () => call(token, id, itemId),
      (updated) => {
        setItems((current) => current.map((i) => (i.id === itemId ? updated : i)));
        void load(); // refresh the goal's server-computed progress percentage
      },
    );
  }

  async function onDeleteItem(itemId: string) {
    if (!window.confirm("Delete this roadmap item?")) {
      return;
    }
    const token = await getToken();
    try {
      await deleteRoadmapItem(token, id, itemId);
      setItems((current) => current.filter((i) => i.id !== itemId));
      setGoal((current) => (current ? { ...current, roadmapItemCount: current.roadmapItemCount - 1 } : current));
    } catch (error) {
      setActionError(formError(error));
    }
  }

  if (loading) {
    return (
      <DashboardShell nav={NAV} navLabel="Applicant dashboard">
        <div className="space-y-4">
          <Skeleton className="h-10 w-1/2" />
          <Skeleton className="h-40 w-full" />
          <Skeleton className="h-40 w-full" />
        </div>
      </DashboardShell>
    );
  }

  if (loadError || !goal) {
    return (
      <DashboardShell nav={NAV} navLabel="Applicant dashboard">
        <Alert tone="error" title="Couldn't load this goal">
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
        <Link href="/dashboard/career-gps" className="inline-flex items-center gap-1.5 text-small font-medium text-foreground-secondary hover:text-foreground">
          <ArrowLeft className="size-4" />
          All career goals
        </Link>

        <Card className="space-y-4 p-6">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div className="space-y-1">
              <h1 className="text-h2 text-foreground">{goal.targetRole}</h1>
              {goal.targetJobTitle ? (
                <p className="text-body text-foreground-secondary">
                  Targeting: {goal.targetJobTitle} at {goal.targetJobCompanyName}
                </p>
              ) : (
                <p className="text-small text-muted-foreground">No linked job — this is a manual plan.</p>
              )}
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <Badge tone={STATUS_TONE[goal.status]} variant="subtle">
                {goal.status}
              </Badge>
              <Button size="sm" variant={goal.isPrimary ? "secondary" : "outline"} onClick={() => void onTogglePrimary()}>
                {goal.isPrimary ? "Primary goal" : "Set as primary"}
              </Button>
            </div>
          </div>

          {goal.roadmapItemCount > 0 ? (
            <div className="space-y-1">
              <div className="h-2 w-full rounded-full bg-muted">
                <div className="h-2 rounded-full bg-primary transition-[width]" style={{ width: `${goal.progressPercent}%` }} />
              </div>
              <p className="text-caption text-muted-foreground">
                {goal.completedRoadmapItemCount} of {goal.roadmapItemCount} roadmap items complete ({goal.progressPercent}%)
              </p>
            </div>
          ) : null}

          {skillGap ? (
            <div className="space-y-2 rounded-lg border border-border bg-surface-subtle p-4 text-small">
              <p className="font-medium text-foreground">
                Skill gap for {skillGap.jobTitle} — {skillGap.matchQuality === "Unscored" ? "not yet scoreable" : `${Math.round((skillGap.skillCoverage ?? 0) * 100)}% coverage`}
              </p>
              {skillGap.matchedSkills.length > 0 ? (
                <p>
                  <span className="font-medium text-foreground">You already have: </span>
                  <span className="text-foreground-secondary">{skillGap.matchedSkills.join(", ")}</span>
                </p>
              ) : null}
              {skillGap.skillsNotYetDemonstrated.length > 0 ? (
                <p>
                  <span className="font-medium text-foreground">Consider adding to your roadmap: </span>
                  <span className="text-muted-foreground">{skillGap.skillsNotYetDemonstrated.join(", ")}</span>
                </p>
              ) : null}
            </div>
          ) : null}

          {actionError ? <p className="text-small text-error">{actionError}</p> : null}

          <div className="flex flex-wrap gap-2 border-t border-border pt-4">
            {goal.status !== "Completed" ? (
              <Button size="sm" variant="outline" onClick={() => void onStatusAction("complete")}>
                Mark goal complete
              </Button>
            ) : null}
            {goal.status !== "Archived" ? (
              <Button size="sm" variant="outline" onClick={() => void onStatusAction("archive")}>
                Archive
              </Button>
            ) : (
              <Button size="sm" variant="outline" onClick={() => void onStatusAction("reactivate")}>
                Reactivate
              </Button>
            )}
            <Button size="sm" variant="ghost" className="text-error" onClick={() => void onDeleteGoal()}>
              <Trash2 className="size-4" />
              Delete goal
            </Button>
          </div>
        </Card>

        <div className="space-y-4">
          <h2 className="text-h3 text-foreground">Roadmap</h2>

          <Card className="p-4">
            <form onSubmit={onAddItem} className="flex flex-wrap items-end gap-3">
              <div className="min-w-[220px] flex-1">
                <FormField id="itemTitle" label="New roadmap item" error={itemFieldErrors.title?.[0]}>
                  {(props) => (
                    <Input {...props} value={newTitle} onChange={(e) => setNewTitle(e.target.value)} placeholder="e.g. Build a portfolio project" required />
                  )}
                </FormField>
              </div>
              <div className="min-w-[150px]">
                <FormField id="itemDate" label="Target date (optional)">
                  {(props) => <Input {...props} type="date" value={newDate} onChange={(e) => setNewDate(e.target.value)} />}
                </FormField>
              </div>
              <div className="min-w-[140px]">
                <span className="mb-1.5 block text-small font-medium text-foreground">Priority</span>
                <Select value={newPriority} onValueChange={(v) => setNewPriority(v as RoadmapItemPriority)}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Low">Low</SelectItem>
                    <SelectItem value="Medium">Medium</SelectItem>
                    <SelectItem value="High">High</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <Button type="submit" disabled={addingItem || !newTitle.trim()}>
                {addingItem ? "Adding…" : "Add item"}
              </Button>
            </form>
          </Card>

          {items.length === 0 ? (
            <EmptyState icon={<ListTodo className="size-6" />} title="No roadmap items yet" description="Add your first step above." />
          ) : (
            <ul className="space-y-3">
              {items.map((item) => {
                const StatusIcon = ITEM_STATUS_ICON[item.status];
                return (
                  <li key={item.id}>
                    <Card className="flex flex-wrap items-center gap-3 p-4">
                      <StatusIcon className={`size-5 shrink-0 ${item.status === "Completed" ? "text-success" : "text-muted-foreground"}`} />
                      <div className="min-w-0 flex-1 space-y-1">
                        <p className={`text-body font-medium text-foreground ${item.status === "Completed" ? "line-through text-muted-foreground" : ""}`}>
                          {item.title}
                        </p>
                        <div className="flex flex-wrap items-center gap-2 text-caption text-muted-foreground">
                          <Badge tone={PRIORITY_TONE[item.priority]} variant="subtle">
                            {item.priority}
                          </Badge>
                          {item.targetDate ? <span>Due {item.targetDate}</span> : null}
                        </div>
                      </div>
                      <div className="flex shrink-0 items-center gap-2">
                        {item.status === "NotStarted" ? (
                          <Button size="sm" variant="outline" onClick={() => void onItemAction(item.id, "start")}>
                            Start
                          </Button>
                        ) : null}
                        {item.status !== "Completed" ? (
                          <Button size="sm" variant="outline" onClick={() => void onItemAction(item.id, "complete")}>
                            Complete
                          </Button>
                        ) : (
                          <Button size="sm" variant="outline" onClick={() => void onItemAction(item.id, "reopen")}>
                            Reopen
                          </Button>
                        )}
                        <Button size="sm" variant="ghost" className="text-error" onClick={() => void onDeleteItem(item.id)}>
                          <Trash2 className="size-4" />
                        </Button>
                      </div>
                    </Card>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      </div>
    </DashboardShell>
  );
}
