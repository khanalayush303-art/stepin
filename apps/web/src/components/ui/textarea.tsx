import * as React from "react";
import { cn } from "@/lib/utils";

export interface TextareaProps
  extends React.TextareaHTMLAttributes<HTMLTextAreaElement> {
  invalid?: boolean;
}

export const Textarea = React.forwardRef<HTMLTextAreaElement, TextareaProps>(
  function Textarea({ className, invalid, rows = 4, ...props }, ref) {
    return (
      <textarea
        ref={ref}
        rows={rows}
        aria-invalid={invalid || undefined}
        className={cn(
          "flex w-full rounded-md border bg-surface p-3 text-body text-foreground",
          "placeholder:text-muted-foreground",
          "focus-visible:outline-2 focus-visible:outline-offset-0 focus-visible:outline-ring",
          "disabled:cursor-not-allowed disabled:bg-muted disabled:opacity-60",
          invalid ? "border-error border-[1.5px]" : "border-input hover:border-border-strong",
          className
        )}
        {...props}
      />
    );
  }
);
