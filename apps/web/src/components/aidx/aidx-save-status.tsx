import type { ReactNode } from "react";
import { CheckCircle2, CircleDashed, AlertCircle } from "lucide-react";
import { cn } from "@/lib/utils";

export type AidxSaveState = "idle" | "dirty" | "saving" | "saved" | "error";

/**
 * A small, always-visible indicator of save state, shared by every AIDX authoring form (admin and
 * workspace) so "did my edit actually save" is never left to a toast that might have been missed.
 */
export function AidxSaveStatus({ state, errorMessage }: { state: AidxSaveState; errorMessage?: string | null }) {
  if (state === "idle") return null;

  const config: Record<Exclude<AidxSaveState, "idle">, { icon: ReactNode; label: string; tone: string }> = {
    dirty: { icon: <CircleDashed aria-hidden="true" className="size-4" />, label: "Unsaved changes", tone: "text-muted-foreground" },
    saving: { icon: <CircleDashed aria-hidden="true" className="size-4 animate-spin" />, label: "Saving…", tone: "text-muted-foreground" },
    saved: { icon: <CheckCircle2 aria-hidden="true" className="size-4" />, label: "Saved", tone: "text-success" },
    error: { icon: <AlertCircle aria-hidden="true" className="size-4" />, label: errorMessage ?? "Couldn't save", tone: "text-error" },
  };

  const { icon, label, tone } = config[state];

  return (
    <span role="status" aria-live="polite" className={cn("inline-flex items-center gap-1.5 text-small font-medium", tone)}>
      {icon}
      {label}
    </span>
  );
}
