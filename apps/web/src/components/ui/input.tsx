import * as React from "react";
import { cn } from "@/lib/utils";

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  invalid?: boolean;
}

/** Single-line input. Always rendered inside a FormField, never bare. */
export const Input = React.forwardRef<HTMLInputElement, InputProps>(
  function Input({ className, invalid, type = "text", ...props }, ref) {
    return (
      <input
        ref={ref}
        type={type}
        aria-invalid={invalid || undefined}
        className={cn(
          "flex h-control-md w-full rounded-md border bg-surface px-4 text-body text-foreground",
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
