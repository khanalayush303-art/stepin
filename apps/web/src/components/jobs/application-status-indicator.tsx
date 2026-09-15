import { Check, Circle, Clock } from "lucide-react";
import { cn } from "@/lib/utils";
import type { ApplicationStage } from "@/lib/types";

/**
 * ApplicationStatusIndicator — mirrors the Figma component of the same name.
 * Stage state is carried by three cues at once (glyph, fill weight, label), so
 * it reads without colour. Rendered as an ordered list with aria-current.
 */
export function ApplicationStatusIndicator({ stages }: { stages: ApplicationStage[] }) {
  return (
    <ol className="flex flex-col gap-6 sm:flex-row sm:gap-0">
      {stages.map((stage, i) => {
        const isLast = i === stages.length - 1;
        return (
          <li
            key={stage.name}
            className="flex flex-1 gap-3 sm:flex-col sm:gap-2"
            aria-current={stage.state === "current" ? "step" : undefined}
          >
            <div className="flex flex-col items-center sm:w-full sm:flex-row">
              <span
                className={cn(
                  "flex size-7 shrink-0 items-center justify-center rounded-full",
                  stage.state === "done" && "bg-success text-on-brand",
                  stage.state === "current" &&
                    "bg-primary text-on-brand ring-4 ring-primary-subtle",
                  stage.state === "todo" &&
                    "border-[1.5px] border-border-strong bg-surface text-muted-foreground"
                )}
                aria-hidden="true"
              >
                {stage.state === "done" ? (
                  <Check className="size-4" strokeWidth={2.5} />
                ) : stage.state === "current" ? (
                  <Clock className="size-4" />
                ) : (
                  <Circle className="size-1.5 fill-current" />
                )}
              </span>

              {!isLast ? (
                <span
                  className={cn(
                    "w-0.5 flex-1 sm:h-0.5 sm:w-auto",
                    stage.state === "done" ? "bg-success" : "bg-border"
                  )}
                  aria-hidden="true"
                />
              ) : null}
            </div>

            <div className="pb-2 sm:pb-0">
              <p
                className={cn(
                  "text-small font-medium",
                  stage.state === "todo" ? "text-muted-foreground" : "text-foreground"
                )}
              >
                {stage.name}
              </p>
              <p
                className={cn(
                  "text-caption",
                  stage.state === "current" ? "text-primary" : "text-muted-foreground"
                )}
              >
                {stage.state === "current" ? `Current${stage.date ? ` · ${stage.date}` : ""}` : stage.date ?? "—"}
              </p>
            </div>
          </li>
        );
      })}
    </ol>
  );
}
