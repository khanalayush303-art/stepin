import * as React from "react";
import { cn } from "@/lib/utils";

export interface EmptyStateProps {
  icon: React.ReactNode;
  title: string;
  description: string;
  action?: React.ReactNode;
  tone?: "empty" | "error";
  className?: string;
}

/** Every empty/error state names what happened, why, and the one way forward. */
export function EmptyState({
  icon,
  title,
  description,
  action,
  tone = "empty",
  className,
}: EmptyStateProps) {
  return (
    <div
      role={tone === "error" ? "alert" : undefined}
      className={cn(
        "flex flex-col items-center gap-3 rounded-lg border border-border bg-card px-6 py-12 text-center",
        className
      )}
    >
      <span
        className={cn(
          "flex size-14 items-center justify-center rounded-full",
          tone === "error" ? "bg-error-subtle text-error" : "bg-muted text-muted-foreground"
        )}
        aria-hidden="true"
      >
        {icon}
      </span>
      <h3 className="text-h4 text-foreground">{title}</h3>
      <p className="max-w-md text-small text-muted-foreground">{description}</p>
      {action ? <div className="mt-1">{action}</div> : null}
    </div>
  );
}
