"use client";

import * as React from "react";
import { Bookmark } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Button } from "@/components/ui/button";
import { formError } from "@/lib/auth/client";
import { saveJob, unsaveJob } from "@/lib/savedJobs/api";
import { cn } from "@/lib/utils";

export interface SaveJobButtonProps {
  jobId: string;
  saved: boolean;
  /** Called only after the server confirms the change — never predicted optimistically, same convention as the recruiter status picker. */
  onSavedChange: (saved: boolean) => void;
  size?: "sm" | "md";
  className?: string;
}

/**
 * A focused save/unsave toggle for a single job. Used on the discover list,
 * the job detail page, and the saved-jobs page itself. Deliberately has no
 * knowledge of where it's rendered — the parent owns the saved-jobs list and
 * is told about changes via onSavedChange, matching the lift-state-up
 * pattern already used by the recruiter application detail page's status picker.
 */
export function SaveJobButton({ jobId, saved, onSavedChange, size = "sm", className }: SaveJobButtonProps) {
  const { getToken } = useAuth();
  const [pending, setPending] = React.useState(false);
  const [error, setError] = React.useState<string>();

  async function onClick(e: React.MouseEvent<HTMLButtonElement>) {
    // Discover-page cards use a stretched-link pattern (Card's title Link has
    // an absolutely-positioned ::after covering the whole card); this button
    // already sits above it via a higher stacking context, but stopping
    // propagation here is a harmless extra guard against ever navigating
    // instead of toggling.
    e.preventDefault();
    e.stopPropagation();

    setPending(true);
    setError(undefined);
    try {
      const token = await getToken();
      if (saved) {
        await unsaveJob(token, jobId);
        onSavedChange(false);
      } else {
        await saveJob(token, jobId);
        onSavedChange(true);
      }
    } catch (err) {
      setError(formError(err));
    } finally {
      setPending(false);
    }
  }

  return (
    <div className={cn("relative z-10", className)}>
      <Button
        type="button"
        variant={saved ? "secondary" : "outline"}
        size={size}
        loading={pending}
        loadingText={saved ? "Removing…" : "Saving…"}
        onClick={(e) => void onClick(e)}
        aria-pressed={saved}
        aria-label={saved ? "Remove this role from your saved jobs" : "Save this role"}
      >
        <Bookmark className={saved ? "fill-current" : undefined} aria-hidden="true" />
        {saved ? "Saved" : "Save"}
      </Button>
      {error ? (
        <p role="alert" className="absolute top-full left-0 mt-1 w-max max-w-[200px] text-caption text-error">
          {error}
        </p>
      ) : null}
    </div>
  );
}
